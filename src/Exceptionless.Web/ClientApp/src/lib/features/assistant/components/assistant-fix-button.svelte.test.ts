import { fireEvent, render, screen } from '@testing-library/svelte';
import { describe, expect, it, vi } from 'vitest';

import { ASSISTANT_CONTROLS_CONTEXT_KEY, type AssistantControls } from '../controls.svelte';
import AssistantFixButton from './assistant-fix-button.svelte';

function renderButton(controls: AssistantControls, resource: 'event' | 'stack', prepareContext = vi.fn()) {
    return render(AssistantFixButton, {
        context: new Map([[ASSISTANT_CONTROLS_CONTEXT_KEY, controls]]),
        props: { prepareContext, resource }
    });
}

describe('AssistantFixButton', () => {
    it('prepares stack context before asking Exie for a fix', async () => {
        const calls: string[] = [];
        const ask = vi.fn((prompt: string) => calls.push(`ask:${prompt}`));
        const prepareContext = vi.fn(() => calls.push('prepare'));
        renderButton({ ask, enabled: () => true }, 'stack', prepareContext);

        await fireEvent.click(screen.getByRole('button', { name: 'Fix this stack with Exie' }));

        expect(calls[0]).toBe('prepare');
        expect(ask).toHaveBeenCalledWith(expect.stringContaining('Analyze this stack'));
        expect(ask).toHaveBeenCalledWith(expect.stringContaining('concrete, prioritized next steps'));
    });

    it('asks Exie to use both the event and stack context', async () => {
        const ask = vi.fn();
        renderButton({ ask, enabled: () => true }, 'event');

        await fireEvent.click(screen.getByRole('button', { name: 'Fix this event with Exie' }));

        expect(ask).toHaveBeenCalledWith(expect.stringContaining('event and its stack context'));
    });

    it('is hidden when the assistant feature is disabled', () => {
        renderButton({ ask: vi.fn(), enabled: () => false }, 'stack');

        expect(screen.queryByRole('button', { name: 'Fix this stack with Exie' })).toBeNull();
    });
});
