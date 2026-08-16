import { describe, expect, it } from 'vitest';

import type { AssistantChatMessage } from './models';

import { createAssistantChatRequest } from './assistant-request';

describe('createAssistantChatRequest', () => {
    it('sends the complete retained conversation so another app instance can continue it', () => {
        const messages: AssistantChatMessage[] = [
            { content: 'First question', id: 'user-1', role: 'user', tools: [] },
            {
                content: 'First answer',
                feedback: 'helpful',
                id: 'assistant-1',
                role: 'assistant',
                tools: [{ arguments: '{}', id: 'tool-1', name: 'search_stacks', result: '{}', status: 'complete' }]
            },
            {
                content: 'Follow-up question',
                id: 'user-2',
                isSuggestedAction: true,
                role: 'user',
                suggestedActionLabel: 'Follow up',
                suggestedActionPath: '/next/stack/source-stack',
                tools: []
            }
        ];

        expect(createAssistantChatRequest(messages, 'conversation-id', 'organization-id', '/next/stack/stack-id', 'project-id')).toEqual({
            conversation_id: 'conversation-id',
            messages: [
                { content: 'First question', role: 'user' },
                { content: 'First answer', role: 'assistant' },
                {
                    content: 'Follow-up question',
                    is_suggested_action: true,
                    role: 'user',
                    suggested_action_label: 'Follow up',
                    suggested_action_path: '/next/stack/source-stack'
                }
            ],
            organization_id: 'organization-id',
            path: '/next/stack/stack-id',
            project_id: 'project-id'
        });
    });
});
