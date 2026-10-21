import type { AssistantChatMessage, AssistantConversation, AssistantToolActivity } from './models';

const STORAGE_PREFIX = 'exie:conversations:v1:';
/** Shares the history prefix so signing out clears it too. */
const ACTIVE_PREFIX = `${STORAGE_PREFIX}active:`;
const MAX_CONVERSATIONS = 20;
/** Saved chats can hold event details, so they are not kept forever. */
const MAX_CONVERSATION_AGE_MS = 30 * 24 * 60 * 60 * 1000;
/** Keeps Exie's history well under the origin's storage quota, which it shares with the sign-in token. */
const MAX_STORED_CHARACTERS = 1_000_000;
const MAX_TITLE_LENGTH = 80;
/** Tool results above this size keep only the fields Exie needs to link resources. */
const MAX_TOOL_RESULT_LENGTH = 16_000;
const RESOURCE_FIELDS = ['id', 'name', 'stack_id', 'title', 'webUrl'];

/** Removes every stored Exie conversation, for example when the user signs out. */
export function clearAssistantConversations(storage = getStorage()): void {
    if (!storage) {
        return;
    }

    const keys = Array.from({ length: storage.length }, (_, index) => storage.key(index));
    for (const key of keys) {
        if (key?.startsWith(STORAGE_PREFIX)) {
            storage.removeItem(key);
        }
    }
}

export function deleteAssistantConversation(scope: string, id: string, storage = getStorage()): AssistantConversation[] {
    const conversations = loadAssistantConversations(scope, storage).filter((conversation) => conversation.id !== id);
    writeConversations(scope, conversations, storage);
    return conversations;
}

/**
 * The chat to reopen on the next visit: an id, null after the user started a new chat, or undefined when
 * nothing was recorded, which reopens the most recent chat.
 */
export function getActiveAssistantConversationId(scope: string, storage = getStorage()): null | string | undefined {
    const value = storage?.getItem(`${ACTIVE_PREFIX}${scope}`);
    return value === null || value === undefined ? undefined : value || null;
}

/** Conversations are stored per user and organization so switching either never mixes histories. */
export function getAssistantConversationScope(userId: string | undefined, organizationId: string | undefined): string | undefined {
    return userId && organizationId ? `${userId}:${organizationId}` : undefined;
}

export function getAssistantConversationTitle(messages: AssistantChatMessage[]): string {
    const firstPrompt = messages.find((message) => message.role === 'user');
    const title = (firstPrompt?.suggestedActionLabel ?? firstPrompt?.content ?? 'New conversation').replace(/\s+/g, ' ').trim();
    return title.length > MAX_TITLE_LENGTH ? `${title.slice(0, MAX_TITLE_LENGTH - 1).trimEnd()}…` : title;
}

export function loadAssistantConversations(scope: string, storage = getStorage()): AssistantConversation[] {
    const value = storage?.getItem(`${STORAGE_PREFIX}${scope}`);
    if (!value) {
        return [];
    }

    let stored: unknown;
    try {
        stored = JSON.parse(value);
    } catch {
        return [];
    }

    const oldestUpdatedAt = Date.now() - MAX_CONVERSATION_AGE_MS;
    const conversations = Array.isArray(stored)
        ? stored.filter((conversation): conversation is AssistantConversation => isConversation(conversation) && conversation.updatedAt >= oldestUpdatedAt)
        : [];
    // Expired chats must leave storage too, not just the list, since they can hold event details.
    if (Array.isArray(stored) && conversations.length < stored.length) {
        writeConversations(scope, conversations, storage);
    }

    return conversations;
}

/** Saves a conversation as the most recent one and returns the updated history, newest first. */
export function saveAssistantConversation(scope: string, conversation: AssistantConversation, storage = getStorage()): AssistantConversation[] {
    const compacted: AssistantConversation = {
        ...conversation,
        messages: conversation.messages.map(compactMessage)
    };
    const conversations = [compacted, ...loadAssistantConversations(scope, storage).filter((item) => item.id !== conversation.id)]
        .sort((left, right) => right.updatedAt - left.updatedAt)
        .slice(0, MAX_CONVERSATIONS);

    writeConversations(scope, conversations, storage);
    return conversations;
}

export function setActiveAssistantConversationId(scope: string, id: null | string, storage = getStorage()): void {
    try {
        storage?.setItem(`${ACTIVE_PREFIX}${scope}`, id ?? '');
    } catch {
        // Storage is full; the most recent chat reopens instead.
    }
}

function compactMessage(message: AssistantChatMessage): AssistantChatMessage {
    return {
        ...message,
        tools: message.tools.map(compactTool)
    };
}

function compactTool(tool: AssistantToolActivity): AssistantToolActivity {
    if (!tool.result || tool.result.length <= MAX_TOOL_RESULT_LENGTH) {
        return tool;
    }

    try {
        const root = JSON.parse(tool.result) as { data?: unknown };
        const data = root.data;
        const compactData = Array.isArray((data as { items?: unknown })?.items)
            ? { items: ((data as { items: unknown[] }).items ?? []).map(pickResourceFields) }
            : pickResourceFields(data);
        return { ...tool, result: JSON.stringify({ data: compactData, truncated: true }) };
    } catch {
        return { ...tool, result: undefined };
    }
}

function getStorage(): Storage | undefined {
    try {
        return typeof localStorage === 'undefined' ? undefined : localStorage;
    } catch {
        return undefined;
    }
}

function isConversation(value: unknown): value is AssistantConversation {
    const conversation = value as null | Partial<AssistantConversation>;
    return (
        typeof conversation === 'object' &&
        conversation !== null &&
        typeof conversation.id === 'string' &&
        typeof conversation.title === 'string' &&
        typeof conversation.updatedAt === 'number' &&
        (conversation.projectId === undefined || conversation.projectId === null || typeof conversation.projectId === 'string') &&
        Array.isArray(conversation.messages) &&
        conversation.messages.every((message) => typeof message?.id === 'string' && Array.isArray(message.parts) && Array.isArray(message.tools))
    );
}

function pickResourceFields(value: unknown): unknown {
    if (typeof value !== 'object' || value === null) {
        return undefined;
    }

    return Object.fromEntries(Object.entries(value).filter(([key, fieldValue]) => RESOURCE_FIELDS.includes(key) && typeof fieldValue === 'string'));
}

function withoutToolResults(conversation: AssistantConversation): AssistantConversation {
    return {
        ...conversation,
        messages: conversation.messages.map((message) => ({
            ...message,
            tools: message.tools.map((tool) => ({ ...tool, result: undefined }))
        }))
    };
}

function writeConversations(scope: string, conversations: AssistantConversation[], storage: Storage | undefined): void {
    if (!storage) {
        return;
    }

    const key = `${STORAGE_PREFIX}${scope}`;
    if (conversations.length === 0) {
        storage.removeItem(key);
        return;
    }

    let remaining = conversations;
    let hasStrippedNewest = false;
    while (remaining.length > 0) {
        const value = JSON.stringify(remaining);
        if (value.length <= MAX_STORED_CHARACTERS) {
            try {
                storage.setItem(key, value);
                return;
            } catch {
                // Storage is full: shrink the history below and try again.
            }
        }

        if (remaining.length > 1) {
            remaining = remaining.slice(0, -1);
        } else if (!hasStrippedNewest) {
            remaining = [withoutToolResults(remaining[0]!)];
            hasStrippedNewest = true;
        } else {
            // The newest chat cannot fit on its own, so keep the history that was last saved.
            return;
        }
    }
}
