export type { AssistantConversationSharingSettings } from '#generated/api.ts';

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
    conversationId?: string;
    /** Why the response ended early. Absent for a response that completed normally. */
    endReason?: AssistantMessageEndReason;
    errorMessage?: string;
    feedback?: AssistantFeedback;
    feedbackReason?: AssistantFeedbackReason;
    id: string;
    isSuggestedAction?: boolean;
    /** Text and tool activity in the order they streamed. */
    parts: AssistantMessagePart[];
    role: 'assistant' | 'user';
    suggestedActionLabel?: string;
    suggestedActionPath?: string;
    suggestedActions?: AssistantSuggestedAction[];
    tools: AssistantToolActivity[];
}

export interface AssistantConversation {
    id: string;
    messages: AssistantChatMessage[];
    /** The project Exie searched, or null for all projects. Chats saved before scopes were recorded omit it. */
    projectId?: null | string;
    title: string;
    updatedAt: number;
}

export type AssistantFeedback = 'helpful' | 'not-helpful';

export type AssistantFeedbackReason = 'incomplete' | 'incorrect' | 'not-relevant' | 'other' | 'wrong-action';

export type AssistantMessageEndReason = 'error' | 'stopped';

export type AssistantMessagePart = { text: string; type: 'text' } | { toolId: string; type: 'tool' };

/** A project Exie can be scoped to from the composer. */
export interface AssistantProject {
    id: string;
    name: string;
}

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
