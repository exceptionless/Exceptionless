import { render, screen } from '@testing-library/svelte';
import { describe, expect, it, vi } from 'vitest';

vi.mock('katex/dist/katex.min.css', () => ({}));

import type { AssistantChatMessage } from '../models';

import AssistantMessage from './assistant-message.svelte';

describe('AssistantMessage', () => {
    it('keeps tool research in activity details without attaching resource cards', () => {
        const message: AssistantChatMessage = {
            content: 'The timeout stack is the best issue to investigate next.',
            id: 'assistant-message',
            role: 'assistant',
            tools: [
                {
                    arguments: '{"sort":"-total_occurrences"}',
                    id: 'tool-call',
                    name: 'search_stacks',
                    result: JSON.stringify({
                        data: {
                            items: [
                                {
                                    id: 'stack-1',
                                    title: 'Timeout expired.',
                                    webUrl: '/next/stack/stack-1'
                                }
                            ]
                        },
                        ok: true
                    }),
                    status: 'complete'
                }
            ]
        };

        render(AssistantMessage, { props: { message } });

        expect(screen.getByText('The timeout stack is the best issue to investigate next.')).not.toBeNull();
        expect(screen.getByText('Searched error stacks')).not.toBeNull();
        expect(screen.queryByRole('link', { name: /Timeout expired/ })).toBeNull();
    });
});
