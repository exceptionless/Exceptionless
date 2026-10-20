import type { AssistantChatMessage, AssistantConversation, AssistantToolActivity } from './models';

const STORAGE_PREFIX = 'exie:conversations:v1:';
const MAX_CONVERSATIONS = 20;
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

    try {
        const conversations = JSON.parse(value) as unknown;
        return Array.isArray(conversations) ? conversations.filter(isConversation) : [];
    } catch {
        return [];
    }
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

function writeConversations(scope: string, conversations: AssistantConversation[], storage: Storage | undefined): void {
    if (!storage) {
        return;
    }

    const key = `${STORAGE_PREFIX}${scope}`;
    let remaining = conversations;
    while (remaining.length > 0) {
        const value = JSON.stringify(remaining);
        if (value.length > MAX_STORED_CHARACTERS) {
            remaining = remaining.slice(0, -1);
            continue;
        }

        try {
            storage.setItem(key, value);
            return;
        } catch {
            // Storage is full: drop the oldest conversation and try again.
            remaining = remaining.slice(0, -1);
        }
    }

    storage.removeItem(key);
}
