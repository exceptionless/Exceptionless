export interface AssistantAccess {
    enabled: boolean;
    has_access: boolean;
    message?: string;
    minimum_plan_id?: string;
    upgrade_required: boolean;
}

export type AssistantAccessState = 'available' | 'disabled' | 'error' | 'loading' | 'upgrade-required';

export interface AssistantChatMessage {
    content: string;
    feedback?: AssistantFeedback;
    id: string;
    isSuggestedAction?: boolean;
    role: 'assistant' | 'user';
    suggestedActionLabel?: string;
    suggestedActionPath?: string;
    suggestedActions?: AssistantSuggestedAction[];
    tools: AssistantToolActivity[];
}

export type AssistantFeedback = 'helpful' | 'not-helpful';

export interface AssistantPromptRequest {
    id: string;
    prompt: string;
}

export interface AssistantSuggestedAction {
    href?: string;
    label: string;
    prompt: string;
    sourcePath?: string;
}

export interface AssistantToolActivity {
    arguments: string;
    id: string;
    name: string;
    result?: string;
    status: 'cancelled' | 'complete' | 'failed' | 'running';
}
