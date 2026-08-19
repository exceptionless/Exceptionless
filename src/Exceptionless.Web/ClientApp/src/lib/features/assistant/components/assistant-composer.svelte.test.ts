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

    it('shows and runs the tools command while typing a slash command', async () => {
        const onSubmit = vi.fn();
        render(AssistantComposer, { props: { onStop: vi.fn(), onSubmit, value: '/' } });

        const commandMenu = screen.getByLabelText('Exie commands');
        expect(commandMenu.textContent).toContain('/tools');
        expect(commandMenu.textContent).toContain('Show tool calls in the conversation');

        await fireEvent.click(screen.getByText('/tools'));

        expect(onSubmit).toHaveBeenCalledWith('/tools');
    });
});
