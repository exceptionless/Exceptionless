import { Exceptionless } from '@exceptionless/browser';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';

vi.mock('$app/environment', () => ({ browser: true }));
vi.mock('@exceptionless/browser', async () => {
    const { Configuration, ExceptionlessClient } = await import('@exceptionless/core');
    const config = new Configuration();
    config.apiKey = 'local-test-key';
    config.serverUrl = 'https://localhost';
    config.defaultTags.push('UI', 'Svelte');
    config.updateSettingsWhenIdleInterval = 0;
    // Exercise real event builders and plugins, without starting timers or sending events.
    config.services.queue.enqueue = vi.fn().mockResolvedValue(undefined);
    config.services.queue.startup = vi.fn().mockResolvedValue(undefined);
    config.services.queue.process = vi.fn().mockResolvedValue(undefined);
    return { Exceptionless: new ExceptionlessClient(config) };
});

import { configureSessions, endSession, setUserIdentity, submitFeatureUsage, submitLog } from './exceptionless-session';

describe('Exceptionless session events', () => {
    beforeEach(async () => {
        vi.useFakeTimers();
        vi.spyOn(Exceptionless, 'submitSessionEnd').mockResolvedValue(undefined);
        await endSession();
        configureSessions(Exceptionless.config);
        vi.mocked(Exceptionless.config.services.queue.enqueue).mockClear();
    });

    afterEach(() => {
        vi.clearAllTimers();
        vi.useRealTimers();
        vi.restoreAllMocks();
    });

    it('skips anonymous startup and resume sessions while retaining ordinary diagnostics', async () => {
        await Exceptionless.startup();
        await Exceptionless.startup();
        await submitLog('api-failure', 'HTTP 500');
        await submitFeatureUsage('login');

        const events = vi.mocked(Exceptionless.config.services.queue.enqueue).mock.calls.map(([event]) => event);
        expect(events.map((event) => event.type)).toEqual(['log', 'usage']);
    });

    it('starts an identified session once when the user loads and keeps identity on resume', async () => {
        await setUserIdentity('session-user@example.test', 'Session User');
        const sessionId = Exceptionless.config.currentSessionIdentifier;
        expect(sessionId).toMatch(/^[a-f0-9]{32}$/);
        await setUserIdentity('session-user@example.test', 'Updated Name');
        expect(Exceptionless.config.services.queue.enqueue).toHaveBeenCalledOnce();
        expect(Exceptionless.config.currentSessionIdentifier).toBe(sessionId);

        await Exceptionless.startup();
        const events = vi.mocked(Exceptionless.config.services.queue.enqueue).mock.calls.map(([event]) => event);
        expect(events).toHaveLength(2);
        expect(events.every((event) => event.type === 'session' && event.data?.['@user']?.identity === 'session-user@example.test')).toBe(true);
        expect(Exceptionless.config.currentSessionIdentifier).toMatch(/^[a-f0-9]{32}$/);
    });

    it('does not clear a newer identity when an earlier logout finishes', async () => {
        await setUserIdentity('previous-user');
        const previousSessionId = Exceptionless.config.currentSessionIdentifier;
        let finishSessionEnd: () => void = () => {};
        vi.mocked(Exceptionless.submitSessionEnd).mockImplementationOnce(
            () =>
                new Promise<void>((resolve) => {
                    finishSessionEnd = resolve;
                })
        );
        const ending = endSession();
        await vi.waitFor(() => expect(Exceptionless.submitSessionEnd).toHaveBeenCalledWith(previousSessionId));
        await setUserIdentity('next-user');
        const nextSessionId = Exceptionless.config.currentSessionIdentifier;
        finishSessionEnd();
        await ending;

        expect(Exceptionless.config.defaultData['@user']).toMatchObject({ identity: 'next-user' });
        expect(nextSessionId).not.toBe(previousSessionId);
        expect(Exceptionless.config.currentSessionIdentifier).toBe(nextSessionId);
    });

    it('clears the heartbeat identity on logout and suppresses anonymous resume sessions', async () => {
        await setUserIdentity('logging-out-user');
        await endSession();
        vi.mocked(Exceptionless.config.services.queue.enqueue).mockClear();
        await Exceptionless.startup();

        expect(Exceptionless.config.currentSessionIdentifier).toBeNull();
        expect(Exceptionless.config.services.queue.enqueue).not.toHaveBeenCalled();
    });

    it('keeps usage metadata and feedback attached to the existing user session', async () => {
        await setUserIdentity('exie-test-user');
        const properties = { exie: { conversation_id: 'conversation-1', role: 'user' } };
        await submitFeatureUsage('assistant.MessageSent', properties);
        await submitFeatureUsage('assistant.ResponseHelpful', properties);

        const events = vi.mocked(Exceptionless.config.services.queue.enqueue).mock.calls.map(([event]) => event);
        expect(events).toHaveLength(3);
        expect(events[0]).toMatchObject({ type: 'session' });
        expect(events[1]).toMatchObject({
            data: properties,
            source: 'assistant.MessageSent',
            type: 'usage'
        });
        expect(events[2]).toMatchObject({ data: properties, source: 'assistant.ResponseHelpful', type: 'usage' });
        for (const event of events) {
            expect(event.message).toBeUndefined();
            expect(event.data?.['@user']).toMatchObject({ identity: 'exie-test-user' });
            expect(event.tags).toEqual(expect.arrayContaining(['UI', 'Svelte']));
        }
    });

    it('preserves ordinary feature usage submissions without extended data', async () => {
        await submitFeatureUsage('project.Created');
        expect(Exceptionless.config.services.queue.enqueue).toHaveBeenCalledOnce();
        expect(Exceptionless.config.services.queue.enqueue).toHaveBeenCalledWith(expect.objectContaining({ source: 'project.Created', type: 'usage' }));
    });

    it('skips assistant events until an identified session exists', async () => {
        await submitFeatureUsage('assistant.Opened');
        await submitLog('assistant.Prompt', 'Before identity');
        expect(Exceptionless.config.services.queue.enqueue).not.toHaveBeenCalled();

        await setUserIdentity('identified@example.test');
        await submitFeatureUsage('assistant.Opened');
        const events = vi.mocked(Exceptionless.config.services.queue.enqueue).mock.calls.map(([event]) => event);
        expect(events.at(-1)?.data?.['@user']?.identity).toBe('identified@example.test');
        expect(events.at(-1)?.data?.['@ref:session']).toBe(Exceptionless.config.currentSessionIdentifier);
    });

    it('attaches explicitly enabled transcript logs to the existing session', async () => {
        await setUserIdentity('exie-test-user');
        const properties = { exie: { conversation_id: 'conversation-1', role: 'user' } };
        await submitLog('assistant.Prompt', 'Why did checkout fail?', properties);
        const events = vi.mocked(Exceptionless.config.services.queue.enqueue).mock.calls.map(([event]) => event);
        expect(events.at(-1)).toMatchObject({ data: properties, message: 'Why did checkout fail?', source: 'assistant.Prompt', type: 'log' });
        expect(events.at(-1)?.data?.['@user']).toMatchObject({ identity: 'exie-test-user' });
    });
});
