import { describe, expect, it } from 'vitest';

import type { AssistantChatMessage } from './models';

import { applyAssistantStreamEvent, endAssistantMessage, getAssistantMessageSegments } from './assistant-message-parts';

function emptyMessage(): AssistantChatMessage {
    return { content: '', id: 'assistant-1', parts: [], role: 'assistant', tools: [] };
}

describe('applyAssistantStreamEvent', () => {
    it('keeps streamed text and tool calls in arrival order', () => {
        let message = emptyMessage();
        message = applyAssistantStreamEvent(message, { text: 'Let me ', type: 'text_delta' }, '/');
        message = applyAssistantStreamEvent(message, { text: 'check.', type: 'text_delta' }, '/');
        message = applyAssistantStreamEvent(message, { arguments: '{}', tool_call_id: 'tool-1', tool_name: 'search_stacks', type: 'tool_call' }, '/');
        message = applyAssistantStreamEvent(message, { result: '{"ok":true}', tool_call_id: 'tool-1', type: 'tool_result' }, '/');
        message = applyAssistantStreamEvent(message, { text: 'Found it.', type: 'text_delta' }, '/');

        expect(message.content).toBe('Let me check.Found it.');
        expect(message.parts).toEqual([
            { text: 'Let me check.', type: 'text' },
            { toolId: 'tool-1', type: 'tool' },
            { text: 'Found it.', type: 'text' }
        ]);
        expect(message.tools[0]?.status).toBe('complete');
    });

    it('marks failed tool results and stream errors on the message', () => {
        let message = applyAssistantStreamEvent(emptyMessage(), { arguments: '{}', tool_call_id: 'tool-1', tool_name: 'get_stack', type: 'tool_call' }, '/');
        message = applyAssistantStreamEvent(message, { result: '{"ok":false}', tool_call_id: 'tool-1', type: 'tool_result' }, '/');
        message = applyAssistantStreamEvent(message, { message: 'Provider unavailable.', type: 'error' }, '/');

        expect(message.tools[0]?.status).toBe('failed');
        expect(message.endReason).toBe('error');
        expect(message.errorMessage).toBe('Provider unavailable.');
    });

    it('records the request path on suggested actions', () => {
        const message = applyAssistantStreamEvent(
            emptyMessage(),
            { suggested_actions: [{ label: 'Next', prompt: 'Do next' }], type: 'suggested_actions' },
            '/stack/1'
        );

        expect(message.suggestedActions).toEqual([{ label: 'Next', prompt: 'Do next', sourcePath: '/stack/1' }]);
    });
});

describe('endAssistantMessage', () => {
    it('cancels running tools when a response is stopped', () => {
        const message = applyAssistantStreamEvent(emptyMessage(), { arguments: '{}', tool_call_id: 'tool-1', tool_name: 'get_stack', type: 'tool_call' }, '/');

        const stopped = endAssistantMessage(message, 'stopped');

        expect(stopped.endReason).toBe('stopped');
        expect(stopped.errorMessage).toBeUndefined();
        expect(stopped.tools[0]?.status).toBe('cancelled');
    });
});

describe('getAssistantMessageSegments', () => {
    it('groups adjacent tool calls into one trace and skips blank text', () => {
        const message: AssistantChatMessage = {
            ...emptyMessage(),
            parts: [
                { text: '\n', type: 'text' },
                { toolId: 'tool-1', type: 'tool' },
                { toolId: 'tool-2', type: 'tool' },
                { text: 'Answer', type: 'text' }
            ],
            tools: [
                { arguments: '{}', id: 'tool-1', name: 'list_projects', status: 'complete' },
                { arguments: '{}', id: 'tool-2', name: 'search_stacks', status: 'complete' }
            ]
        };

        const segments = getAssistantMessageSegments(message);

        expect(segments.map((segment) => segment.type)).toEqual(['tools', 'text']);
        expect(segments[0]?.type === 'tools' && segments[0].tools.map((tool) => tool.id)).toEqual(['tool-1', 'tool-2']);
    });
});
