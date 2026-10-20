import { fireEvent, render, screen } from '@testing-library/svelte';
import { beforeEach, describe, expect, it, vi } from 'vitest';

import AssistantComposer from './assistant-composer.svelte';

describe('AssistantComposer', () => {
    beforeEach(() => {
        HTMLElement.prototype.scrollIntoView = vi.fn();
    });

    it('submits with Enter and preserves Shift+Enter for multiline prompts', async () => {
        const onSubmit = vi.fn();
        render(AssistantComposer, { props: { onStop: vi.fn(), onSubmit, value: 'Investigate this' } });
        const textarea = screen.getByRole('textbox', { name: 'Message Exie' });

        await fireEvent.keyDown(textarea, { key: 'Enter' });
        await fireEvent.keyDown(textarea, { key: 'Enter', shiftKey: true });

        expect(onSubmit).toHaveBeenCalledOnce();
        expect(onSubmit).toHaveBeenCalledWith('Investigate this');
    });

    it('shows a stop action while Exie is streaming', async () => {
        const onStop = vi.fn();
        render(AssistantComposer, { props: { isStreaming: true, onStop, onSubmit: vi.fn() } });

        await fireEvent.click(screen.getByRole('button', { name: 'Stop generating' }));

        expect(onStop).toHaveBeenCalledOnce();
    });

    it('shows the page context and lets the user leave it out', async () => {
        const view = render(AssistantComposer, {
            props: { context: { kind: 'stack', title: 'NullReferenceException in Checkout' }, onStop: vi.fn(), onSubmit: vi.fn() }
        });

        const chip = screen.getByTestId('assistant-context-chip');
        expect(chip.textContent).toContain('Stack:');
        expect(chip.textContent).toContain('NullReferenceException in Checkout');
        expect(screen.getByRole('textbox', { name: 'Message Exie' }).getAttribute('placeholder')).toBe('Ask about this stack…');

        await fireEvent.click(screen.getByRole('button', { name: 'Don’t use this stack as context' }));

        expect(screen.queryByTestId('assistant-context-chip')).toBeNull();
        await fireEvent.click(screen.getByRole('button', { name: 'Use this stack as context' }));
        expect(screen.getByTestId('assistant-context-chip')).toBeTruthy();
        view.unmount();
    });

    it('cancels editing a message with Escape', async () => {
        const onCancelEdit = vi.fn();
        render(AssistantComposer, { props: { isEditing: true, onCancelEdit, onStop: vi.fn(), onSubmit: vi.fn(), value: 'Original question' } });

        expect(screen.getByText('Editing message')).toBeTruthy();
        await fireEvent.keyDown(screen.getByRole('textbox', { name: 'Message Exie' }), { key: 'Escape' });

        expect(onCancelEdit).toHaveBeenCalledOnce();
    });

    it('does not submit an empty prompt', async () => {
        const onSubmit = vi.fn();
        render(AssistantComposer, { props: { onStop: vi.fn(), onSubmit, value: '   ' } });

        await fireEvent.keyDown(screen.getByRole('textbox', { name: 'Message Exie' }), { key: 'Enter' });

        expect(onSubmit).not.toHaveBeenCalled();
        expect(screen.getByRole('button', { name: 'Send message' }).hasAttribute('disabled')).toBe(true);
    });
});
