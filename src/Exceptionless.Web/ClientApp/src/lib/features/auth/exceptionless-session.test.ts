import { Exceptionless } from '@exceptionless/browser';
import { beforeEach, describe, expect, it, vi } from 'vitest';

vi.mock('$app/environment', () => ({ browser: true }));
vi.mock('@exceptionless/browser', async () => {
    const { Configuration, ExceptionlessClient } = await import('@exceptionless/core');
    const config = new Configuration();
    config.apiKey = 'local-test-key';
    config.serverUrl = 'https://localhost';
    config.defaultTags.push('UI', 'Svelte');
    config.useSessions(false);
    // Exercise real event builders and plugins, without starting timers or sending events.
    config.services.queue.enqueue = vi.fn().mockResolvedValue(undefined);
    return { Exceptionless: new ExceptionlessClient(config) };
});

import { setUserIdentity, submitFeatureUsage, submitLog } from './exceptionless-session';

describe('Exceptionless session events', () => {
    beforeEach(() => {
        vi.mocked(Exceptionless.config.services.queue.enqueue).mockClear();
    });

    it('keeps transcript logs and feedback attached to the existing user session', async () => {
        await setUserIdentity('exie-test-user');
        const properties = { exie: { conversation_id: 'conversation-1', role: 'user' } };
        await submitLog('assistant.MessageSent', 'Why did checkout fail?', properties);
        await submitFeatureUsage('assistant.ResponseHelpful', properties);

        const events = vi.mocked(Exceptionless.config.services.queue.enqueue).mock.calls.map(([event]) => event);
        expect(events).toHaveLength(3);
        expect(events[0]).toMatchObject({ type: 'session' });
        expect(events[1]).toMatchObject({
            data: properties,
            message: 'Why did checkout fail?',
            source: 'assistant.MessageSent',
            type: 'log'
        });
        expect(events[2]).toMatchObject({ data: properties, source: 'assistant.ResponseHelpful', type: 'usage' });
        for (const event of events) {
            expect(event.data?.['@user']).toMatchObject({ identity: 'exie-test-user' });
            expect(event.tags).toEqual(expect.arrayContaining(['UI', 'Svelte']));
        }
    });

    it('preserves ordinary feature usage submissions without extended data', async () => {
        await submitFeatureUsage('project.Created');
        expect(Exceptionless.config.services.queue.enqueue).toHaveBeenCalledOnce();
        expect(Exceptionless.config.services.queue.enqueue).toHaveBeenCalledWith(expect.objectContaining({ source: 'project.Created', type: 'usage' }));
    });
});
