import type { AssistantChatMessage, AssistantConversation, AssistantToolActivity } from './models';

const STORAGE_PREFIX = 'exie:conversations:v1:';
/** Shares the history prefix so signing out clears it too. */
const ACTIVE_PREFIX = `${STORAGE_PREFIX}active:`;
const MAX_CONVERSATIONS = 20;
/** Saved chats can hold event details, so they are not kept forever. */
const MAX_CONVERSATION_AGE_MS = 30 * 24 * 60 * 60 * 1000;
/**
 * One budget for every user and organization's history together, so belonging to many organizations cannot fill
 * the origin's storage quota, which Exie shares with the sign-in token.
 */
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
    return writeConversations(scope, conversations, storage);
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

/** Saves a conversation as the most recent one and returns the history as stored, newest first. */
export function saveAssistantConversation(scope: string, conversation: AssistantConversation, storage = getStorage()): AssistantConversation[] {
    const compacted: AssistantConversation = {
        ...conversation,
        messages: conversation.messages.map(compactMessage)
    };
    const conversations = [compacted, ...loadAssistantConversations(scope, storage).filter((item) => item.id !== conversation.id)]
        .sort((left, right) => right.updatedAt - left.updatedAt)
        .slice(0, MAX_CONVERSATIONS);

    // The stored history can be shorter than this list once older chats are dropped for space.
    return writeConversations(scope, conversations, storage);
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

/** Finds the oldest chat in any history except the one being saved now, which must be kept. */
function findOldestConversation(histories: Map<string, AssistantConversation[]>, keep: AssistantConversation): undefined | { index: number; key: string } {
    let oldest: undefined | { index: number; key: string; updatedAt: number };
    for (const [key, conversations] of histories) {
        conversations.forEach((conversation, index) => {
            if (conversation !== keep && (!oldest || conversation.updatedAt < oldest.updatedAt)) {
                oldest = { index, key, updatedAt: conversation.updatedAt };
            }
        });
    }

    return oldest && { index: oldest.index, key: oldest.key };
}

function getStorage(): Storage | undefined {
    try {
        return typeof localStorage === 'undefined' ? undefined : localStorage;
    } catch {
        return undefined;
    }
}

function getStoredSize(histories: Map<string, AssistantConversation[]>): number {
    let size = 0;
    for (const conversations of histories.values()) {
        size += conversations.length > 0 ? JSON.stringify(conversations).length : 0;
    }

    return size;
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

/** Reads another scope's history as stored, without the expiry cleanup that loading performs. */
function readStoredHistories(storage: Storage, exceptKey: string): Map<string, AssistantConversation[]> {
    const histories = new Map<string, AssistantConversation[]>();
    for (let index = 0; index < storage.length; index++) {
        const key = storage.key(index);
        if (!key?.startsWith(STORAGE_PREFIX) || key.startsWith(ACTIVE_PREFIX) || key === exceptKey) {
            continue;
        }

        histories.set(key, readStoredHistory(storage, key));
    }

    return histories;
}

function readStoredHistory(storage: Storage, key: string): AssistantConversation[] {
    try {
        const stored = JSON.parse(storage.getItem(key) ?? '[]') as unknown;
        return Array.isArray(stored) ? stored.filter(isConversation) : [];
    } catch {
        return [];
    }
}

/** Saves a history, dropping its oldest chats if the browser's storage is full for other reasons, and returns what was saved. */
function trySetItem(storage: Storage, key: string, history: AssistantConversation[], keep: AssistantConversation): AssistantConversation[] | undefined {
    let remaining = history;
    while (remaining.length > 0) {
        try {
            storage.setItem(key, JSON.stringify(remaining));
            return remaining;
        } catch {
            // Storage is full: drop the oldest chat, but never the one being saved now.
            const oldestIndex = remaining.reduce(
                (oldest, conversation, index) =>
                    conversation !== keep && (oldest < 0 || conversation.updatedAt < remaining[oldest]!.updatedAt) ? index : oldest,
                -1
            );
            if (oldestIndex < 0) {
                return undefined;
            }

            remaining = remaining.filter((_, index) => index !== oldestIndex);
        }
    }

    return undefined;
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

/** Writes a scope's history within the shared budget and returns what is stored for that scope afterward. */
function writeConversations(scope: string, conversations: AssistantConversation[], storage: Storage | undefined): AssistantConversation[] {
    if (!storage) {
        return conversations;
    }

    const key = `${STORAGE_PREFIX}${scope}`;
    if (conversations.length === 0) {
        storage.removeItem(key);
        return [];
    }

    // Every scope shares one budget, so the oldest chats anywhere make room before this scope's newest chat is dropped.
    const histories = readStoredHistories(storage, key);
    // A copy, since evicting chats must not change the list the caller keeps in memory.
    histories.set(key, [...conversations]);
    const changedKeys = new Set([key]);
    let newest = conversations[0]!;
    while (getStoredSize(histories) > MAX_STORED_CHARACTERS) {
        const oldest = findOldestConversation(histories, newest);
        if (oldest) {
            histories.get(oldest.key)!.splice(oldest.index, 1);
            changedKeys.add(oldest.key);
        } else if (newest.messages.some((message) => message.tools.some((tool) => tool.result !== undefined))) {
            newest = withoutToolResults(newest);
            histories.set(key, [newest]);
        } else {
            // The newest chat cannot fit on its own, so keep the history that was last saved.
            return readStoredHistory(storage, key);
        }
    }

    // Write the scopes that gave up chats first, which frees space before this scope grows. If this scope still
    // cannot be saved, put the other scopes back so a failed save never costs older chats.
    const previous = new Map([...changedKeys].map((changedKey) => [changedKey, storage.getItem(changedKey)]));
    for (const changedKey of [...changedKeys].filter((item) => item !== key)) {
        const history = histories.get(changedKey)!;
        if (history.length === 0) {
            storage.removeItem(changedKey);
        } else {
            try {
                storage.setItem(changedKey, JSON.stringify(history));
            } catch {
                // A shorter history fits where the longer one did; if it does not, the scope keeps its previous chats.
            }
        }
    }

    const saved = trySetItem(storage, key, histories.get(key)!, newest);
    if (saved) {
        return saved;
    }

    for (const [changedKey, value] of previous) {
        if (value === null) {
            storage.removeItem(changedKey);
        } else {
            storage.setItem(changedKey, value);
        }
    }

    return readStoredHistory(storage, key);
}
