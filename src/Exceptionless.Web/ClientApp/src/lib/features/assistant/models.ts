export interface AssistantAccess {
    enabled: boolean;
    has_access: boolean;
    message?: string;
    upgrade_required: boolean;
}

export interface AssistantChatMessage {
    content: string;
    feedback?: AssistantFeedback;
    id: string;
    role: 'assistant' | 'user';
    suggestedActions?: AssistantSuggestedAction[];
    tools: AssistantToolActivity[];
}

export type AssistantFeedback = 'helpful' | 'not-helpful';

export interface AssistantSuggestedAction {
    label: string;
    prompt: string;
}

export interface AssistantToolActivity {
    arguments: string;
    id: string;
    name: string;
    result?: string;
    status: 'complete' | 'failed' | 'running';
}
