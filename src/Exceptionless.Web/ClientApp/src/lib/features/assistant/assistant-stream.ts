export interface AssistantStreamEvent {
    arguments?: string;
    message?: string;
    result?: string;
    text?: string;
    tool_call_id?: string;
    tool_name?: string;
    type: 'done' | 'error' | 'text_delta' | 'tool_call' | 'tool_result';
}

export async function readAssistantStream(stream: ReadableStream<Uint8Array>, onEvent: (event: AssistantStreamEvent) => Promise<void> | void): Promise<void> {
    const reader = stream.getReader();
    const decoder = new TextDecoder();
    let buffer = '';

    while (true) {
        const { done, value } = await reader.read();
        buffer += decoder.decode(value, { stream: !done });
        const lines = buffer.split('\n');
        buffer = done ? '' : (lines.pop() ?? '');

        for (const line of lines) {
            if (line.trim()) {
                await onEvent(JSON.parse(line) as AssistantStreamEvent);
            }
        }

        if (done) {
            if (buffer.trim()) {
                await onEvent(JSON.parse(buffer) as AssistantStreamEvent);
            }

            break;
        }
    }
}
