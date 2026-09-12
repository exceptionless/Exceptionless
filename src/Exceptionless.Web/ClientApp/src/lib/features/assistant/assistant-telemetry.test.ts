import { beforeEach, describe, expect, it, vi } from 'vitest';

const { submitFeatureUsage, submitLog } = vi.hoisted(() => ({
    submitFeatureUsage: vi.fn(() => Promise.resolve()),
    submitLog: vi.fn(() => Promise.resolve())
}));
vi.mock('$features/auth/exceptionless-session', () => ({ submitFeatureUsage, submitLog }));

import { AssistantTurnTelemetry, trackAssistantEvent } from './assistant-telemetry';

const context = {
    assistant_message_id: 'response-1',
    conversation_id: 'conversation-1',
    mode: 'sheet' as const,
    organization_id: 'organization-1',
    user_message_id: 'prompt-1'
};

describe('Exie session telemetry', () => {
    beforeEach(() => {
        submitFeatureUsage.mockReset();
        submitLog.mockReset();
    });

    it('records one prompt and one response outcome without conversation or tool content', () => {
        const turn = new AssistantTurnTelemetry(context, 25, 'composer');
        turn.observe({ text: 'The checkout ', type: 'text_delta' });
        turn.observe({ arguments: '{"secret":"tool-argument"}', tool_call_id: 'tool-1', tool_name: 'get_event', type: 'tool_call' });
        turn.observe({ result: '{"ok":true,"data":{"secret":"tool-result"}}', tool_call_id: 'tool-1', type: 'tool_result' });
        turn.observe({ text: 'request timed out.', type: 'text_delta' });
        turn.observe({ type: 'done' });

        expect(turn.finish()).toBe('completed');
        expect(turn.finish()).toBeUndefined();
        expect(submitFeatureUsage).toHaveBeenCalledTimes(2);
        expect(submitLog).not.toHaveBeenCalled();
        expect(submitFeatureUsage).toHaveBeenNthCalledWith(1, 'assistant.MessageSent', {
            exie: expect.objectContaining({ ...context, message_characters: 25, prompt_source: 'composer', role: 'user' })
        });
        expect(submitFeatureUsage).toHaveBeenLastCalledWith('assistant.ResponseCompleted', {
            exie: expect.objectContaining({ ...context, outcome: 'completed', response_characters: 31, role: 'assistant', tool_calls: 1, tool_failures: 0 })
        });
        const telemetry = JSON.stringify(submitFeatureUsage.mock.calls);
        expect(telemetry).not.toContain('checkout');
        expect(telemetry).not.toContain('tool-argument');
        expect(telemetry).not.toContain('tool-result');
    });

    it('records a streamed failure even when a done event follows it', () => {
        const turn = new AssistantTurnTelemetry(context, 11, 'starter');
        turn.observe({ text: 'Partial answer', type: 'text_delta' });
        turn.observe({ message: 'Exie took too long.', type: 'error' });
        turn.observe({ type: 'done' });
        expect(turn.finish()).toBe('failed');
        expect(submitFeatureUsage).toHaveBeenLastCalledWith('assistant.ResponseFailed', {
            exie: expect.objectContaining({ error_message: 'Exie took too long.', reason: 'stream_error' })
        });
        expect(JSON.stringify(submitFeatureUsage.mock.calls)).not.toContain('Partial answer');
    });

    it('distinguishes stopped responses and ignores late content after cancellation', () => {
        const turn = new AssistantTurnTelemetry(context, 11, 'composer');
        turn.observe({ text: 'Partial', type: 'text_delta' });
        expect(turn.finish('organization_changed')).toBe('cancelled');
        turn.observe({ text: 'Late content', type: 'text_delta' });
        turn.observe({ type: 'done' });
        turn.finish();
        expect(submitFeatureUsage).toHaveBeenCalledTimes(2);
        expect(submitFeatureUsage).toHaveBeenLastCalledWith('assistant.ResponseCancelled', {
            exie: expect.objectContaining({ organization_id: 'organization-1', reason: 'organization_changed' })
        });
    });

    it('does not count a silently interrupted stream as a successful response', () => {
        const turn = new AssistantTurnTelemetry(context, 11, 'composer');
        turn.observe({ text: 'Partial', type: 'text_delta' });
        expect(turn.finish()).toBe('failed');
        expect(submitFeatureUsage).toHaveBeenLastCalledWith('assistant.ResponseFailed', {
            exie: expect.objectContaining({ reason: 'incomplete_stream', received_done: false })
        });
    });

    it('bounds full logging while keeping accurate message counts and emitting one assembled response', () => {
        const content = 'x'.repeat(20_000);
        const turn = new AssistantTurnTelemetry(context, content.length, 'retry', { previous_conversation_id: 'previous-conversation' });
        turn.enableFullLogging(content);
        turn.enableFullLogging(content);
        turn.observe({ text: content, type: 'text_delta' });
        turn.observe({ type: 'done' });
        turn.finish();
        expect(submitFeatureUsage).toHaveBeenNthCalledWith(1, 'assistant.MessageSent', {
            exie: expect.objectContaining({ message_characters: 20_000, previous_conversation_id: 'previous-conversation' })
        });
        expect(submitFeatureUsage).toHaveBeenLastCalledWith('assistant.ResponseCompleted', {
            exie: expect.objectContaining({ message_characters: 20_000, response_characters: 20_000 })
        });
        expect(submitFeatureUsage).toHaveBeenCalledTimes(2);
        expect(JSON.stringify(submitFeatureUsage.mock.calls)).not.toContain('xxx');
        expect(submitLog).toHaveBeenCalledTimes(2);
        expect(submitLog).toHaveBeenNthCalledWith(1, 'assistant.Prompt', content.slice(0, 16_384), {
            exie: expect.objectContaining({ ...context, message_characters: 20_000, message_truncated: true, prompt_source: 'retry' })
        });
        expect(submitLog).toHaveBeenLastCalledWith('assistant.Response', content.slice(0, 16_384), {
            exie: expect.objectContaining({ ...context, message_characters: 20_000, message_truncated: true, outcome: 'completed' })
        });
    });

    it('keeps chat interactions working when telemetry cannot be submitted', async () => {
        submitFeatureUsage.mockRejectedValueOnce(new Error('offline'));
        submitFeatureUsage.mockRejectedValueOnce(new Error('offline'));
        submitLog.mockRejectedValueOnce(new Error('offline'));
        expect(() => trackAssistantEvent('assistant.ResponseHelpful', context)).not.toThrow();
        expect(() => new AssistantTurnTelemetry(context, 11, 'composer').enableFullLogging('Investigate')).not.toThrow();
        await Promise.resolve();
        expect(submitFeatureUsage).toHaveBeenCalledTimes(2);
        expect(submitLog).toHaveBeenCalledOnce();
    });
});
