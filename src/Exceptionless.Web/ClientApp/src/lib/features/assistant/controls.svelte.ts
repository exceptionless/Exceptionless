import { getContext, setContext } from 'svelte';

export interface AssistantControls {
    ask: (prompt: string) => void;
    enabled: () => boolean;
}

export type AssistantFixResource = 'event' | 'stack';

export const ASSISTANT_CONTROLS_CONTEXT_KEY = Symbol.for('exceptionless-assistant-controls');

export function setAssistantControls(controls: AssistantControls): void {
    setContext(ASSISTANT_CONTROLS_CONTEXT_KEY, controls);
}

export function tryUseAssistantControls(): AssistantControls | undefined {
    return getContext<AssistantControls | undefined>(ASSISTANT_CONTROLS_CONTEXT_KEY);
}
