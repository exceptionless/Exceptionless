import type { AssistantChatMessage } from './models';

export interface AssistantChatRequestPayload {
    conversation_id: string;
    messages: Pick<AssistantChatMessage, 'content' | 'role'>[];
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
        messages: messages.map((message) => ({ content: message.content, role: message.role })),
        organization_id: organizationId,
        path,
        project_id: projectId
    };
}
