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

    it('records one prompt and one assembled response with the same conversation context', () => {
        const turn = new AssistantTurnTelemetry(context, 'What happened to checkout?', 'composer');
        turn.observe({ text: 'The checkout ', type: 'text_delta' });
        turn.observe({ arguments: '{"secret":"tool-argument"}', tool_call_id: 'tool-1', tool_name: 'get_event', type: 'tool_call' });
        turn.observe({ result: '{"ok":true,"data":{"secret":"tool-result"}}', tool_call_id: 'tool-1', type: 'tool_result' });
        turn.observe({ text: 'request timed out.', type: 'text_delta' });
        turn.observe({ type: 'done' });

        expect(turn.finish()).toBe('completed');
        expect(turn.finish()).toBeUndefined();
        expect(submitLog).toHaveBeenCalledTimes(2);
        expect(submitLog).toHaveBeenNthCalledWith(1, 'assistant.MessageSent', 'What happened to checkout?', {
            exie: expect.objectContaining({ ...context, prompt_source: 'composer', role: 'user' })
        });
        expect(submitLog).toHaveBeenLastCalledWith('assistant.ResponseCompleted', 'The checkout request timed out.', {
            exie: expect.objectContaining({ ...context, outcome: 'completed', role: 'assistant', tool_calls: 1, tool_failures: 0 })
        });
        expect(JSON.stringify(submitLog.mock.calls)).not.toContain('tool-argument');
        expect(JSON.stringify(submitLog.mock.calls)).not.toContain('tool-result');
    });

    it('records a streamed failure even when a done event follows it', () => {
        const turn = new AssistantTurnTelemetry(context, 'Investigate', 'starter');
        turn.observe({ text: 'Partial answer', type: 'text_delta' });
        turn.observe({ message: 'Exie took too long.', type: 'error' });
        turn.observe({ type: 'done' });
        expect(turn.finish()).toBe('failed');
        expect(submitLog).toHaveBeenLastCalledWith('assistant.ResponseFailed', 'Partial answer', {
            exie: expect.objectContaining({ error_message: 'Exie took too long.', reason: 'stream_error' })
        });
    });

    it('distinguishes stopped responses and ignores late content after cancellation', () => {
        const turn = new AssistantTurnTelemetry(context, 'Investigate', 'composer');
        turn.observe({ text: 'Partial', type: 'text_delta' });
        expect(turn.finish('organization_changed')).toBe('cancelled');
        turn.observe({ text: 'Late content', type: 'text_delta' });
        turn.observe({ type: 'done' });
        turn.finish();
        expect(submitLog).toHaveBeenCalledTimes(2);
        expect(submitLog).toHaveBeenLastCalledWith('assistant.ResponseCancelled', 'Partial', {
            exie: expect.objectContaining({ organization_id: 'organization-1', reason: 'organization_changed' })
        });
    });

    it('does not count a silently interrupted stream as a successful response', () => {
        const turn = new AssistantTurnTelemetry(context, 'Investigate', 'composer');
        turn.observe({ text: 'Partial', type: 'text_delta' });
        expect(turn.finish()).toBe('failed');
        expect(submitLog).toHaveBeenLastCalledWith('assistant.ResponseFailed', 'Partial', {
            exie: expect.objectContaining({ reason: 'incomplete_stream', received_done: false })
        });
    });

    it('bounds transcript size and marks truncation without storing each streamed chunk', () => {
        const content = 'x'.repeat(20_000);
        const turn = new AssistantTurnTelemetry(context, content, 'retry', { previous_conversation_id: 'previous-conversation' });
        turn.observe({ text: content, type: 'text_delta' });
        turn.observe({ type: 'done' });
        turn.finish();
        expect(submitLog).toHaveBeenNthCalledWith(1, 'assistant.MessageSent', content.slice(0, 16_384), {
            exie: expect.objectContaining({ message_characters: 20_000, message_truncated: true, previous_conversation_id: 'previous-conversation' })
        });
        expect(submitLog).toHaveBeenLastCalledWith('assistant.ResponseCompleted', content.slice(0, 16_384), {
            exie: expect.objectContaining({ message_characters: 20_000, message_truncated: true, response_characters: 20_000, response_truncated: true })
        });
    });

    it('keeps chat interactions working when telemetry cannot be submitted', async () => {
        submitFeatureUsage.mockRejectedValueOnce(new Error('offline'));
        submitLog.mockRejectedValueOnce(new Error('offline'));
        expect(() => trackAssistantEvent('assistant.ResponseHelpful', context)).not.toThrow();
        expect(() => new AssistantTurnTelemetry(context, 'Investigate', 'composer')).not.toThrow();
        await Promise.resolve();
        expect(submitFeatureUsage).toHaveBeenCalledOnce();
        expect(submitLog).toHaveBeenCalledOnce();
    });
});
