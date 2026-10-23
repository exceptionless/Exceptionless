import type { AssistantStreamEvent } from './assistant-stream';
import type { AssistantChatMessage, AssistantMessageEndReason, AssistantToolActivity } from './models';

import { assistantToolResultFailed } from './assistant-tool-result';

export type AssistantMessageSegment = { key: string; text: string; type: 'text' } | { key: string; tools: AssistantToolActivity[]; type: 'tools' };

export const DEFAULT_ASSISTANT_ERROR_MESSAGE = 'Exie could not complete this request.';

/** Applies one streamed event, keeping text and tool activity in the order they arrived. */
export function applyAssistantStreamEvent(message: AssistantChatMessage, event: AssistantStreamEvent, requestPath: string): AssistantChatMessage {
    if (event.type === 'text_delta') {
        const text = event.text ?? '';
        if (!text) {
            return message;
        }

        const lastPart = message.parts.at(-1);
        const parts =
            lastPart?.type === 'text'
                ? [...message.parts.slice(0, -1), { text: lastPart.text + text, type: 'text' as const }]
                : [...message.parts, { text, type: 'text' as const }];

        return {
            ...message,
            content: message.content + text,
            parts
        };
    }

    if (event.type === 'tool_call' && event.tool_call_id && event.tool_name) {
        return {
            ...message,
            parts: [...message.parts, { toolId: event.tool_call_id, type: 'tool' }],
            tools: [
                ...message.tools,
                {
                    arguments: event.arguments ?? '{}',
                    id: event.tool_call_id,
                    name: event.tool_name,
                    status: 'running'
                }
            ]
        };
    }

    if (event.type === 'tool_result' && event.tool_call_id) {
        const status = assistantToolResultFailed(event.result) ? ('failed' as const) : ('complete' as const);
        return {
            ...message,
            tools: message.tools.map((tool) =>
                tool.id === event.tool_call_id
                    ? {
                          ...tool,
                          result: event.result,
                          status
                      }
                    : tool
            )
        };
    }

    if (event.type === 'suggested_actions') {
        return {
            ...message,
            suggestedActions: (event.suggested_actions ?? []).map((action) => ({
                ...action,
                sourcePath: requestPath
            }))
        };
    }

    if (event.type === 'error') {
        return endAssistantMessage(message, 'error', event.message ?? DEFAULT_ASSISTANT_ERROR_MESSAGE);
    }

    return message;
}

/** Marks a response that ended early and cancels any tool still waiting on a result. */
export function endAssistantMessage(message: AssistantChatMessage, endReason: AssistantMessageEndReason, errorMessage?: string): AssistantChatMessage {
    return {
        ...message,
        endReason,
        errorMessage: endReason === 'error' ? (errorMessage ?? DEFAULT_ASSISTANT_ERROR_MESSAGE) : undefined,
        tools: message.tools.map((tool) => (tool.status === 'running' ? { ...tool, status: 'cancelled' as const } : tool))
    };
}

/** Groups adjacent tool calls so each run of research renders as one trace. */
export function getAssistantMessageSegments(message: AssistantChatMessage): AssistantMessageSegment[] {
    const toolsById = new Map(message.tools.map((tool) => [tool.id, tool]));
    const segments: AssistantMessageSegment[] = [];

    for (const [index, part] of message.parts.entries()) {
        const previous = segments.at(-1);
        if (part.type === 'text') {
            if (!part.text.trim()) {
                continue;
            }

            if (previous?.type === 'text') {
                previous.text += part.text;
            } else {
                segments.push({ key: `text-${index}`, text: part.text, type: 'text' });
            }

            continue;
        }

        const tool = toolsById.get(part.toolId);
        if (!tool) {
            continue;
        }

        if (previous?.type === 'tools') {
            previous.tools.push(tool);
        } else {
            segments.push({ key: `tools-${part.toolId}`, tools: [tool], type: 'tools' });
        }
    }

    return segments;
}
