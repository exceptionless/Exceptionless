import type { AssistantChatMessage } from './models';

export interface AssistantChatRequestPayload {
    conversation_id: string;
    messages: Array<Pick<AssistantChatMessage, 'content' | 'role'> & { is_suggested_action?: boolean }>;
    organization_id?: string;
    path: string;
    project_id?: string;
}

export function createAssistantChatRequest(
    messages: AssistantChatMessage[],
    conversationId: string,
    organizationId: string | undefined,
    path: string,
    projectId: string | undefined
): AssistantChatRequestPayload {
    return {
        conversation_id: conversationId,
        messages: messages.map((message) => ({
            content: message.content,
            ...(message.isSuggestedAction ? { is_suggested_action: true } : {}),
            role: message.role
        })),
        organization_id: organizationId,
        path,
        project_id: projectId
    };
}
