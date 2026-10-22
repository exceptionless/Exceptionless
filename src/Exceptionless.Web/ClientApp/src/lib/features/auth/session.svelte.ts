import { clearAssistantConversations } from '#features/assistant/conversation-history.ts';
import { shutdownIntercomSession } from '#features/intercom/session.ts';

import { accessToken } from './state.svelte';

export function clearAuthenticationSession(clearedAccessToken: '' | null = null) {
    shutdownIntercomSession();
    // Saved Exie chats can hold event details, so every way of signing out removes them, including an expired session.
    clearAssistantConversations();
    accessToken.current = clearedAccessToken;
}
