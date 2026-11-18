import { fireEvent, render, screen } from '@testing-library/svelte';
import { describe, expect, it, vi } from 'vitest';

vi.mock('katex/dist/katex.min.css', () => ({}));

import type { AssistantChatMessage, AssistantToolActivity } from '../models';

import AssistantMessage from './assistant-message.svelte';

const STACK_ID = '5f0c1d2e3a4b5c6d7e8f9a0b';

function assistantMessage(overrides: Partial<AssistantChatMessage> = {}): AssistantChatMessage {
    const content = overrides.content ?? '';
    return {
        content,
        id: 'assistant-message',
        parts: content ? [{ text: content, type: 'text' }] : [],
        role: 'assistant',
        tools: [],
        ...overrides
    };
}

function searchTool(overrides: Partial<AssistantToolActivity> = {}): AssistantToolActivity {
    return {
        arguments: '{"sort":"-total_occurrences"}',
        id: 'tool-call',
        name: 'search_stacks',
        result: JSON.stringify({
            data: {
                items: [
                    {
                        id: STACK_ID,
                        title: 'Timeout expired',
                        webUrl: `/stack/${STACK_ID}`
                    }
                ]
            },
            ok: true
        }),
        status: 'complete',
        ...overrides
    };
}

describe('AssistantMessage', () => {
    it('links stack titles from completed tool research and shows the research step', () => {
        const message = assistantMessage({
            content: 'Timeout expired is the best issue to investigate next.',
            parts: [
                { toolId: 'tool-call', type: 'tool' },
                { text: 'Timeout expired is the best issue to investigate next.', type: 'text' }
            ],
            tools: [searchTool()]
        });

        render(AssistantMessage, { props: { message } });

        expect(screen.getByLabelText('Exie').textContent).toContain('Timeout expired is the best issue to investigate next.');
        expect(screen.getByText('Searched error stacks')).not.toBeNull();
        const stackLink = screen.getByRole('link', { name: 'Timeout expired' });
        expect(stackLink.getAttribute('href')).toBe(`/stack/${STACK_ID}`);
        const linkClasses = stackLink.className.split(/\s+/);
        expect(linkClasses).toContain('text-foreground');
        expect(linkClasses).not.toContain('text-primary');
    });

    it('restores a tool resource link that the model prefixed with an invented host', () => {
        const content = `See [Timeout expired](https://www.example.com/stack/${STACK_ID}) for details.`;
        const message = assistantMessage({
            content,
            parts: [
                { toolId: 'tool-call', type: 'tool' },
                { text: content, type: 'text' }
            ],
            tools: [searchTool()]
        });

        const { container } = render(AssistantMessage, { props: { message } });

        expect(screen.getByRole('link', { name: 'Timeout expired' }).getAttribute('href')).toBe(`/stack/${STACK_ID}`);
        expect(container.querySelector('button[data-streamdown="link"]')).toBeNull();
    });

    it('opens Exceptionless links in a new tab without asking', async () => {
        const open = vi.spyOn(window, 'open').mockReturnValue(null);
        const message = assistantMessage({ content: 'Read the [docs](https://exceptionless.com/docs).' });

        render(AssistantMessage, { props: { message } });
        await fireEvent.click(screen.getByRole('button', { name: 'docs' }));

        await vi.waitFor(() => expect(open).toHaveBeenCalledWith('https://exceptionless.com/docs', '_blank', 'noreferrer'));
        expect(screen.queryByRole('alertdialog')).toBeNull();
        open.mockRestore();
    });

    it('asks before opening other external links, since answers can repeat untrusted event data', async () => {
        const open = vi.spyOn(window, 'open').mockReturnValue(null);
        const message = assistantMessage({ content: 'Sign in at [the portal](https://login.example.net/reset).' });

        render(AssistantMessage, { props: { message } });
        await fireEvent.click(screen.getByRole('button', { name: 'the portal' }));

        const dialog = await screen.findByRole('alertdialog');
        expect(dialog.textContent).toContain('https://login.example.net/reset');
        expect(open).not.toHaveBeenCalled();

        await fireEvent.click(screen.getByRole('button', { name: 'Open link' }));
        expect(open).toHaveBeenCalledWith('https://login.example.net/reset', '_blank', 'noreferrer');
        open.mockRestore();
    });

    it('shows raw HTML as text so links in it cannot skip the confirmation', () => {
        const message = assistantMessage({ content: 'Click <a href="https://login.example.net/reset">Fix it</a> now.' });

        const { container } = render(AssistantMessage, { props: { message } });

        expect(container.querySelector('a[href="https://login.example.net/reset"]')).toBeNull();
        expect(screen.getByLabelText('Exie').textContent).toContain('<a href="https://login.example.net/reset">Fix it</a>');
    });

    it('does not load remote images from answers', () => {
        const message = assistantMessage({ content: 'Chart: ![usage](https://tracker.example.net/pixel.png?data=secret)' });

        const { container } = render(AssistantMessage, { props: { message } });

        expect(container.querySelector('img[src^="https://tracker.example.net"]')).toBeNull();
    });

    it('shows thinking before anything has streamed', () => {
        render(AssistantMessage, { props: { isStreaming: true, message: assistantMessage() } });

        expect(screen.getByText('Exie is thinking…')).toBeTruthy();
    });

    it('shows what a running tool is doing', () => {
        const message = assistantMessage({
            parts: [{ toolId: 'tool-call', type: 'tool' }],
            tools: [searchTool({ result: undefined, status: 'running' })]
        });

        render(AssistantMessage, { props: { isStreaming: true, message } });

        expect(screen.getByText('Searching error stacks…')).toBeTruthy();
        expect(screen.queryByText('Exie is thinking…')).toBeNull();
    });

    it('keeps text and tool activity in the order they streamed', () => {
        const message = assistantMessage({
            content: 'Let me look.All done.',
            parts: [
                { text: 'Let me look.', type: 'text' },
                { toolId: 'tool-call', type: 'tool' },
                { text: 'All done.', type: 'text' }
            ],
            tools: [searchTool()]
        });

        render(AssistantMessage, { props: { message } });

        const text = screen.getByLabelText('Exie').textContent ?? '';
        expect(text.indexOf('Let me look.')).toBeLessThan(text.indexOf('Searched error stacks'));
        expect(text.indexOf('Searched error stacks')).toBeLessThan(text.indexOf('All done.'));
    });

    it('always shows a change Exie made', () => {
        const message = assistantMessage({
            content: 'Done.',
            parts: [
                { toolId: 'critical', type: 'tool' },
                { text: 'Done.', type: 'text' }
            ],
            tools: [
                {
                    arguments: JSON.stringify({ critical: true, stackId: STACK_ID }),
                    id: 'critical',
                    name: 'set_stack_critical',
                    result: JSON.stringify({ data: { id: STACK_ID, title: 'Timeout expired' }, ok: true }),
                    status: 'complete'
                }
            ]
        });

        render(AssistantMessage, { props: { message } });

        expect(screen.getByText('Marked future events critical')).toBeTruthy();
        expect(screen.getByText('Timeout expired')).toBeTruthy();
    });

    it('explains a response stopped before any output and offers a retry', async () => {
        const onRegenerate = vi.fn();
        render(AssistantMessage, { props: { isLast: true, message: assistantMessage({ endReason: 'stopped' }), onRegenerate } });

        expect(screen.getByText('You stopped Exie before it responded.')).toBeTruthy();
        await fireEvent.click(screen.getByRole('button', { name: 'Retry' }));
        expect(onRegenerate).toHaveBeenCalledOnce();
    });

    it('shows a failed response inline with a retry', async () => {
        const onRegenerate = vi.fn();
        render(AssistantMessage, {
            props: { isLast: true, message: assistantMessage({ endReason: 'error', errorMessage: 'The model is unavailable.' }), onRegenerate }
        });

        expect(screen.getByRole('alert').textContent).toContain('The model is unavailable.');
        await fireEvent.click(screen.getByRole('button', { name: 'Retry' }));
        expect(onRegenerate).toHaveBeenCalledOnce();
    });

    it('shows completed suggested actions and submits their prompts', async () => {
        const onSuggestedAction = vi.fn();
        const message = assistantMessage({
            content: 'The timeout stack is the best issue to investigate next.',
            suggestedActions: [
                {
                    label: 'Inspect recent events',
                    prompt: 'Inspect the most recent events in that timeout stack.'
                }
            ]
        });

        render(AssistantMessage, { props: { message, onSuggestedAction, showSuggestedActions: true } });

        expect(screen.getByLabelText('Suggested next steps')).not.toBeNull();
        await fireEvent.click(screen.getByRole('button', { name: 'Inspect recent events' }));
        expect(onSuggestedAction).toHaveBeenCalledWith({
            label: 'Inspect recent events',
            prompt: 'Inspect the most recent events in that timeout stack.'
        });
    });

    it('does not show suggested actions while the response is streaming', () => {
        const message = assistantMessage({
            content: 'Partial answer',
            suggestedActions: [{ label: 'Inspect events', prompt: 'Inspect recent events.' }]
        });

        render(AssistantMessage, { props: { isStreaming: true, message, showSuggestedActions: true } });

        expect(screen.queryByLabelText('Suggested next steps')).toBeNull();
    });

    it('lets the user edit and resend their message', async () => {
        const onEdit = vi.fn();
        const message: AssistantChatMessage = {
            content: 'What broke?',
            id: 'user-message',
            parts: [{ text: 'What broke?', type: 'text' }],
            role: 'user',
            tools: []
        };

        render(AssistantMessage, { props: { message, onEdit } });

        expect(screen.getByLabelText('You').textContent).toContain('What broke?');
        await fireEvent.click(screen.getByRole('button', { name: 'Edit and resend' }));
        expect(onEdit).toHaveBeenCalledOnce();
    });
});
