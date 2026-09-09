import { fireEvent, render, screen, waitFor } from '@testing-library/svelte';
import { beforeEach, describe, expect, it, vi } from 'vitest';

const toast = vi.hoisted(() => ({ error: vi.fn(), success: vi.fn() }));
vi.mock('svelte-sonner', () => ({ toast }));

import AssistantMessageActions from './assistant-message-actions.svelte';

describe('AssistantMessageActions', () => {
    const writeText = vi.fn(() => Promise.resolve());

    beforeEach(() => {
        writeText.mockClear();
        toast.error.mockClear();
        toast.success.mockClear();
        Object.defineProperty(navigator, 'clipboard', { configurable: true, value: { writeText } });
    });

    it('copies the complete message and regenerates the response', async () => {
        const onRegenerate = vi.fn();
        const onCopy = vi.fn();
        render(AssistantMessageActions, { props: { content: 'The answer', onCopy, onRegenerate } });

        await fireEvent.click(screen.getByRole('button', { name: 'Copy message' }));
        await fireEvent.click(screen.getByRole('button', { name: 'Regenerate response' }));

        expect(writeText).toHaveBeenCalledWith('The answer');
        await waitFor(() => expect(onCopy).toHaveBeenCalledOnce());
        expect(onRegenerate).toHaveBeenCalledOnce();
    });

    it('reports feedback to the conversation owner for correlated telemetry', async () => {
        const onFeedback = vi.fn();
        render(AssistantMessageActions, { props: { content: 'Sensitive answer', onFeedback, showFeedback: true } });

        await fireEvent.click(screen.getByRole('button', { name: 'Good response' }));

        expect(onFeedback).toHaveBeenCalledWith('helpful');
        expect(toast.success).toHaveBeenCalledWith('Marked as helpful.');
    });

    it('disables regenerate while the callback is pending', async () => {
        let resolveRegenerate!: () => void;
        const onRegenerate = vi.fn(() => new Promise<void>((resolve) => (resolveRegenerate = resolve)));
        render(AssistantMessageActions, { props: { content: 'The answer', onRegenerate } });

        await fireEvent.click(screen.getByRole('button', { name: 'Regenerate response' }));

        expect(screen.getByRole('button', { name: 'Regenerating response' }).hasAttribute('disabled')).toBe(true);
        resolveRegenerate();
        await waitFor(() => expect(screen.getByRole('button', { name: 'Regenerate response' }).hasAttribute('disabled')).toBe(false));
    });

    it('reports clearing feedback to the conversation owner', async () => {
        const onFeedback = vi.fn();
        render(AssistantMessageActions, { props: { content: 'The answer', feedback: 'helpful', onFeedback, showFeedback: true } });

        await fireEvent.click(screen.getByRole('button', { name: 'Good response' }));

        expect(onFeedback).toHaveBeenCalledWith(undefined);
        expect(toast.error).not.toHaveBeenCalled();
    });
});
