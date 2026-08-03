import { describe, expect, it } from 'vitest';

import { type AssistantStreamEvent, readAssistantStream } from './assistant-stream';

describe('readAssistantStream', () => {
    it('parses NDJSON events split across transport chunks', async () => {
        const encoder = new TextEncoder();
        const events: AssistantStreamEvent[] = [];
        const stream = new ReadableStream<Uint8Array>({
            start(controller) {
                controller.enqueue(encoder.encode('{"type":"text_delta","text":"Hel'));
                controller.enqueue(encoder.encode('lo"}\n{"type":"tool_call","tool_call_id":"call_1"}\n'));
                controller.enqueue(
                    encoder.encode(
                        '{"type":"suggested_actions","suggested_actions":[{"label":"Inspect events","prompt":"Inspect recent events."}]}\n{"type":"done"}'
                    )
                );
                controller.close();
            }
        });

        await readAssistantStream(stream, (event) => {
            events.push(event);
        });

        expect(events).toEqual([
            { text: 'Hello', type: 'text_delta' },
            { tool_call_id: 'call_1', type: 'tool_call' },
            { suggested_actions: [{ label: 'Inspect events', prompt: 'Inspect recent events.' }], type: 'suggested_actions' },
            { type: 'done' }
        ]);
    });
});
