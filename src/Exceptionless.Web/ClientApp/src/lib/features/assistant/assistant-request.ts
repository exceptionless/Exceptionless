import type { AssistantChatMessage } from './models';

export interface AssistantChatRequestPayload {
    conversation_id: string;
    messages: Array<
        Pick<AssistantChatMessage, 'content' | 'role'> & {
            is_suggested_action?: boolean;
            suggested_action_label?: string;
            suggested_action_path?: string;
        }
    >;
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
            role: message.role,
            ...(message.suggestedActionLabel ? { suggested_action_label: message.suggestedActionLabel } : {}),
            ...(message.suggestedActionPath ? { suggested_action_path: message.suggestedActionPath } : {})
        })),
        organization_id: organizationId,
        path,
        project_id: projectId
    };
}
