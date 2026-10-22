import { beforeEach, describe, expect, it } from 'vitest';

import type { AssistantChatMessage, AssistantConversation } from './models';

import {
    clearAssistantConversations,
    deleteAssistantConversation,
    getActiveAssistantConversationId,
    getAssistantConversationScope,
    getAssistantConversationTitle,
    loadAssistantConversations,
    saveAssistantConversation,
    setActiveAssistantConversationId
} from './conversation-history';

class MemoryStorage implements Storage {
    public get length(): number {
        return this.items.size;
    }

    private readonly items = new Map<string, string>();

    public clear(): void {
        this.items.clear();
    }

    public getItem(key: string): null | string {
        return this.items.get(key) ?? null;
    }

    public key(index: number): null | string {
        return [...this.items.keys()][index] ?? null;
    }

    public removeItem(key: string): void {
        this.items.delete(key);
    }

    public setItem(key: string, value: string): void {
        this.items.set(key, value);
    }
}

let storage: MemoryStorage;

/** Saved chats expire after 30 days, so the relative order is based on the current time. */
function conversation(id: string, order: number, messages: AssistantChatMessage[] = []): AssistantConversation {
    return { id, messages, title: id, updatedAt: Date.now() + order };
}

function userMessage(content: string): AssistantChatMessage {
    return { content, id: crypto.randomUUID(), parts: [{ text: content, type: 'text' }], role: 'user', tools: [] };
}

describe('conversation history', () => {
    beforeEach(() => {
        storage = new MemoryStorage();
    });

    it('scopes history to both the user and the organization', () => {
        expect(getAssistantConversationScope('user-1', 'org-1')).toBe('user-1:org-1');
        expect(getAssistantConversationScope(undefined, 'org-1')).toBeUndefined();
        expect(getAssistantConversationScope('user-1', undefined)).toBeUndefined();
    });

    it('keeps the newest conversations first and replaces an updated one', () => {
        saveAssistantConversation('scope', conversation('a', 1), storage);
        saveAssistantConversation('scope', conversation('b', 2), storage);
        const conversations = saveAssistantConversation('scope', conversation('a', 3), storage);

        expect(conversations.map((item) => item.id)).toEqual(['a', 'b']);
        expect(loadAssistantConversations('scope', storage).map((item) => item.id)).toEqual(['a', 'b']);
        expect(loadAssistantConversations('other-scope', storage)).toEqual([]);
    });

    it('keeps at most twenty conversations', () => {
        for (let index = 0; index < 25; index++) {
            saveAssistantConversation('scope', conversation(`chat-${index}`, index), storage);
        }

        const conversations = loadAssistantConversations('scope', storage);
        expect(conversations).toHaveLength(20);
        expect(conversations[0]?.id).toBe('chat-24');
    });

    it('drops the oldest conversations to stay within the storage budget', () => {
        const answer: AssistantChatMessage = { content: 'x'.repeat(300_000), id: 'a', parts: [], role: 'assistant', tools: [] };
        for (let index = 0; index < 5; index++) {
            saveAssistantConversation('scope', conversation(`chat-${index}`, index, [answer]), storage);
        }

        const conversations = loadAssistantConversations('scope', storage);
        expect(conversations.map((item) => item.id)).toEqual(['chat-4', 'chat-3', 'chat-2']);
        expect(storage.getItem('exie:conversations:v1:scope')!.length).toBeLessThanOrEqual(1_000_000);
    });

    it('shares one storage budget across organizations and drops the oldest chats anywhere first', () => {
        const answer: AssistantChatMessage = { content: 'x'.repeat(300_000), id: 'a', parts: [], role: 'assistant', tools: [] };
        for (let index = 0; index < 3; index++) {
            saveAssistantConversation('user-1:org-a', conversation(`a-${index}`, index, [answer]), storage);
        }
        setActiveAssistantConversationId('user-1:org-a', 'a-2', storage);

        saveAssistantConversation('user-1:org-b', conversation('b-0', 10, [answer]), storage);

        expect(loadAssistantConversations('user-1:org-a', storage).map((item) => item.id)).toEqual(['a-2', 'a-1']);
        expect(loadAssistantConversations('user-1:org-b', storage).map((item) => item.id)).toEqual(['b-0']);
        const total = Array.from({ length: storage.length }, (_, index) => storage.key(index))
            .filter((key) => key?.startsWith('exie:conversations:v1:') && !key.startsWith('exie:conversations:v1:active:'))
            .reduce((size, key) => size + storage.getItem(key!)!.length, 0);
        expect(total).toBeLessThanOrEqual(1_000_000);
        expect(getActiveAssistantConversationId('user-1:org-a', storage)).toBe('a-2');
    });

    it('returns the history as stored after chats are dropped for space', () => {
        const answer: AssistantChatMessage = { content: 'x'.repeat(300_000), id: 'a', parts: [], role: 'assistant', tools: [] };
        for (let index = 0; index < 3; index++) {
            saveAssistantConversation('scope', conversation(`chat-${index}`, index, [answer]), storage);
        }

        const conversations = saveAssistantConversation('scope', conversation('chat-3', 3, [answer]), storage);

        expect(conversations.map((item) => item.id)).toEqual(['chat-3', 'chat-2', 'chat-1']);
        expect(loadAssistantConversations('scope', storage).map((item) => item.id)).toEqual(['chat-3', 'chat-2', 'chat-1']);
    });

    it('keeps other organizations chats when the new chat cannot be saved', () => {
        const answer: AssistantChatMessage = { content: 'x'.repeat(300_000), id: 'a', parts: [], role: 'assistant', tools: [] };
        for (let index = 0; index < 3; index++) {
            saveAssistantConversation('user-1:org-a', conversation(`a-${index}`, index, [answer]), storage);
        }

        // Unrelated data fills the browser's quota, so this organization's history cannot be written at all.
        const setItem = storage.setItem.bind(storage);
        storage.setItem = (key, value) => {
            if (key === 'exie:conversations:v1:user-1:org-b') {
                throw new DOMException('Quota exceeded', 'QuotaExceededError');
            }

            setItem(key, value);
        };

        const conversations = saveAssistantConversation('user-1:org-b', conversation('b-0', 10, [answer]), storage);

        expect(conversations).toEqual([]);
        expect(loadAssistantConversations('user-1:org-a', storage).map((item) => item.id)).toEqual(['a-2', 'a-1', 'a-0']);
    });

    it('removes another organization history once all of its chats are dropped for space', () => {
        const answer: AssistantChatMessage = { content: 'x'.repeat(600_000), id: 'a', parts: [], role: 'assistant', tools: [] };
        saveAssistantConversation('user-1:org-a', conversation('a-0', 0, [answer]), storage);

        saveAssistantConversation('user-1:org-b', conversation('b-0', 10, [answer]), storage);

        expect(storage.getItem('exie:conversations:v1:user-1:org-a')).toBeNull();
        expect(loadAssistantConversations('user-1:org-b', storage).map((item) => item.id)).toEqual(['b-0']);
    });

    it('keeps the saved history when the newest conversation is too large to store', () => {
        saveAssistantConversation('scope', conversation('older', 1, [userMessage('Older question')]), storage);
        const answer: AssistantChatMessage = { content: 'x'.repeat(1_100_000), id: 'a', parts: [], role: 'assistant', tools: [] };

        const conversations = saveAssistantConversation('scope', conversation('huge', 2, [answer]), storage);

        // The returned history is what is stored, so the oversized chat that could not be saved is not offered later.
        expect(conversations.map((item) => item.id)).toEqual(['older']);
        expect(loadAssistantConversations('scope', storage).map((item) => item.id)).toEqual(['older']);
    });

    it('drops tool results from the newest conversation when that is what makes it fit', () => {
        const tool = { arguments: '{}', id: 'tool-1', name: 'get_event', result: 'x'.repeat(15_000), status: 'complete' as const };
        const answer: AssistantChatMessage = {
            content: 'Done',
            id: 'a',
            parts: [],
            role: 'assistant',
            tools: Array.from({ length: 70 }, (_, index) => ({ ...tool, id: `tool-${index}` }))
        };

        saveAssistantConversation('scope', conversation('tools', 1, [answer]), storage);

        const [saved] = loadAssistantConversations('scope', storage);
        expect(saved?.messages[0]?.tools).toHaveLength(70);
        expect(saved?.messages[0]?.tools[0]?.result).toBeUndefined();
    });

    it('drops chats older than 30 days', () => {
        const old = { ...conversation('old', 0), updatedAt: Date.now() - 31 * 24 * 60 * 60 * 1000 };
        storage.setItem('exie:conversations:v1:scope', JSON.stringify([conversation('recent', 0), old]));

        expect(loadAssistantConversations('scope', storage).map((item) => item.id)).toEqual(['recent']);
        const persisted = JSON.parse(storage.getItem('exie:conversations:v1:scope')!) as AssistantConversation[];
        expect(persisted.map((item) => item.id)).toEqual(['recent']);

        storage.setItem('exie:conversations:v1:scope', JSON.stringify([old]));
        expect(loadAssistantConversations('scope', storage)).toEqual([]);
        expect(storage.getItem('exie:conversations:v1:scope')).toBeNull();
    });

    it('drops expired chats from organizations the user no longer opens', () => {
        const expired = { ...conversation('a-old', 0), updatedAt: Date.now() - 31 * 24 * 60 * 60 * 1000 };
        storage.setItem('exie:conversations:v1:user-1:org-a', JSON.stringify([conversation('a-recent', 0), expired]));
        storage.setItem('exie:conversations:v1:user-1:org-c', JSON.stringify([expired]));

        saveAssistantConversation('user-1:org-b', conversation('b-0', 1), storage);

        const orgA = JSON.parse(storage.getItem('exie:conversations:v1:user-1:org-a')!) as AssistantConversation[];
        expect(orgA.map((item) => item.id)).toEqual(['a-recent']);
        expect(storage.getItem('exie:conversations:v1:user-1:org-c')).toBeNull();
    });

    it('remembers which chat to reopen, including a new blank chat', () => {
        expect(getActiveAssistantConversationId('scope', storage)).toBeUndefined();
        setActiveAssistantConversationId('scope', 'chat-1', storage);
        expect(getActiveAssistantConversationId('scope', storage)).toBe('chat-1');
        setActiveAssistantConversationId('scope', null, storage);
        expect(getActiveAssistantConversationId('scope', storage)).toBeNull();

        clearAssistantConversations(storage);
        expect(getActiveAssistantConversationId('scope', storage)).toBeUndefined();
    });

    it('compacts large tool results to the fields needed for resource links', () => {
        const items = Array.from({ length: 400 }, (_, index) => ({
            description: 'x'.repeat(100),
            id: `stack-${index}`,
            title: `Stack ${index}`,
            webUrl: `/stack/${index}`
        }));
        const message: AssistantChatMessage = {
            content: 'Done',
            id: 'assistant-1',
            parts: [],
            role: 'assistant',
            tools: [{ arguments: '{}', id: 'tool-1', name: 'search_stacks', result: JSON.stringify({ data: { items }, ok: true }), status: 'complete' }]
        };

        saveAssistantConversation('scope', conversation('a', 1, [message]), storage);

        const result = JSON.parse(loadAssistantConversations('scope', storage)[0]!.messages[0]!.tools[0]!.result!) as {
            data: { items: Array<Record<string, string>> };
        };
        expect(result.data.items[0]).toEqual({ id: 'stack-0', title: 'Stack 0', webUrl: '/stack/0' });
    });

    it('keeps the stack names and links of large grouped counts', () => {
        const groups = Array.from({ length: 200 }, (_, index) => ({
            events: index,
            key: `stack-${index}`,
            occurrences: index,
            title: `Stack ${index}`,
            trend: Array.from({ length: 5 }, () => ({ date: '2026-10-21T00:00:00Z', events: 1, occurrences: 1 })),
            webUrl: `/stack/stack-${index}`
        }));
        const message: AssistantChatMessage = {
            content: 'Done',
            id: 'assistant-1',
            parts: [],
            role: 'assistant',
            tools: [
                {
                    arguments: '{}',
                    id: 'tool-1',
                    name: 'count_events',
                    result: JSON.stringify({ data: { group_by: 'stack', groups }, ok: true }),
                    status: 'complete'
                }
            ]
        };

        saveAssistantConversation('scope', conversation('a', 1, [message]), storage);

        const result = JSON.parse(loadAssistantConversations('scope', storage)[0]!.messages[0]!.tools[0]!.result!) as {
            data: { groups: Array<Record<string, string>> };
        };
        expect(result.data.groups[0]).toEqual({ key: 'stack-0', title: 'Stack 0', webUrl: '/stack/stack-0' });
    });

    it('ignores corrupt stored data and supports deleting and clearing', () => {
        storage.setItem('exie:conversations:v1:broken', '{not json');
        expect(loadAssistantConversations('broken', storage)).toEqual([]);

        saveAssistantConversation('scope', conversation('a', 1), storage);
        saveAssistantConversation('scope', conversation('b', 2), storage);
        expect(deleteAssistantConversation('scope', 'a', storage).map((item) => item.id)).toEqual(['b']);

        storage.setItem('unrelated', 'kept');
        clearAssistantConversations(storage);
        expect(loadAssistantConversations('scope', storage)).toEqual([]);
        expect(storage.getItem('unrelated')).toBe('kept');
    });

    it('titles a conversation from its first prompt', () => {
        expect(getAssistantConversationTitle([userMessage('  What   broke\nyesterday? ')])).toBe('What broke yesterday?');
        expect(getAssistantConversationTitle([userMessage('x'.repeat(200))])).toHaveLength(80);
        expect(getAssistantConversationTitle([])).toBe('New conversation');
    });
});
