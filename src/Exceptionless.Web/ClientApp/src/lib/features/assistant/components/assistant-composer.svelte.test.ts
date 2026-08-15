import { fireEvent, render, screen } from '@testing-library/svelte';
import { describe, expect, it, vi } from 'vitest';

import AssistantComposer from './assistant-composer.svelte';

describe('AssistantComposer', () => {
    it('submits with Enter and preserves Shift+Enter for multiline prompts', async () => {
        const onSubmit = vi.fn();
        render(AssistantComposer, { props: { onStop: vi.fn(), onSubmit, value: 'Investigate this' } });
        const textarea = screen.getByRole('textbox', { name: 'Message Exie' });

        await fireEvent.keyDown(textarea, { key: 'Enter' });
        await fireEvent.keyDown(textarea, { key: 'Enter', shiftKey: true });

        expect(onSubmit).toHaveBeenCalledOnce();
    });

    it('shows a stop action while Exie is streaming', async () => {
        const onStop = vi.fn();
        render(AssistantComposer, { props: { isStreaming: true, onStop, onSubmit: vi.fn() } });

        await fireEvent.click(screen.getByRole('button', { name: 'Stop generating' }));

        expect(onStop).toHaveBeenCalledOnce();
    });
});
