import { fireEvent, render, screen } from '@testing-library/svelte';
import { describe, expect, it, vi } from 'vitest';

vi.mock('katex/dist/katex.min.css', () => ({}));

import type { AssistantChatMessage } from '../models';

import AssistantMessage from './assistant-message.svelte';

describe('AssistantMessage', () => {
    it('links stack titles from completed tool research', () => {
        const message: AssistantChatMessage = {
            content: 'Timeout expired is the best issue to investigate next.',
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
                                    title: 'Timeout expired',
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

        expect(screen.getByLabelText('Exie').textContent).toContain('Timeout expired is the best issue to investigate next.');
        expect(screen.getByText('Searched error stacks')).not.toBeNull();
        const stackLink = screen.getByRole('link', { name: 'Timeout expired' });
        expect(stackLink.getAttribute('href')).toBe('/next/stack/stack-1');
        const linkClasses = stackLink.className.split(/\s+/);
        expect(linkClasses).toContain('text-foreground');
        expect(linkClasses).not.toContain('text-primary');
    });

    it('shows completed suggested actions and submits their prompts', async () => {
        const onSuggestedAction = vi.fn();
        const message: AssistantChatMessage = {
            content: 'The timeout stack is the best issue to investigate next.',
            id: 'assistant-message',
            role: 'assistant',
            suggestedActions: [
                {
                    label: 'Inspect recent events',
                    prompt: 'Inspect the most recent events in that timeout stack.'
                }
            ],
            tools: []
        };

        render(AssistantMessage, { props: { message, onSuggestedAction } });

        expect(screen.getByLabelText('Suggested actions')).not.toBeNull();
        await fireEvent.click(screen.getByRole('button', { name: 'Inspect recent events' }));
        expect(onSuggestedAction).toHaveBeenCalledWith({
            label: 'Inspect recent events',
            prompt: 'Inspect the most recent events in that timeout stack.'
        });
    });

    it('does not show suggested actions while the response is streaming', () => {
        const message: AssistantChatMessage = {
            content: 'Partial answer',
            id: 'assistant-message',
            role: 'assistant',
            suggestedActions: [{ label: 'Inspect events', prompt: 'Inspect recent events.' }],
            tools: []
        };

        render(AssistantMessage, { props: { isStreaming: true, message } });

        expect(screen.queryByLabelText('Suggested actions')).toBeNull();
    });
});
