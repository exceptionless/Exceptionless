export interface AssistantContext {
    kind: 'event' | 'project' | 'stack';
    title?: string;
}

export interface AssistantStarter {
    label: string;
    prompt: string;
}

/** Starter prompts that fit what the user is looking at. */
export function getAssistantStarters(context: AssistantContext | undefined): AssistantStarter[] {
    switch (context?.kind) {
        case 'event':
            return [
                { label: 'Explain this error', prompt: 'Explain this event and what most likely caused it.' },
                { label: 'How do I fix it?', prompt: 'How do I fix the error in this event? Include a code example if it helps.' },
                { label: 'Is it getting worse?', prompt: 'How often does this stack occur, and is it getting worse?' }
            ];
        case 'project':
            return [
                { label: 'Top errors in this project', prompt: 'What are the top errors in this project over the last 24 hours?' },
                { label: 'What’s new?', prompt: 'Which new stacks appeared in this project in the last 24 hours?' },
                { label: 'Set up a client', prompt: 'How do I configure a client to send events to this project?' }
            ];
        case 'stack':
            return [
                { label: 'Summarize this stack', prompt: 'Summarize this stack: what fails, how often, and who is affected.' },
                { label: 'Why is this happening?', prompt: 'Why is this stack happening? Look at its recent events for clues.' },
                { label: 'How do I fix it?', prompt: 'How do I fix this stack? Include a code example if it helps.' }
            ];
        default:
            return [
                { label: 'Top errors in the last 24 hours', prompt: 'What are my top errors in the last 24 hours?' },
                { label: 'What’s new?', prompt: 'Which new stacks appeared in the last 24 hours?' },
                { label: 'What needs attention?', prompt: 'Which open stacks occurred most recently and need attention?' }
            ];
    }
}
