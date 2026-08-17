import { fireEvent, render, screen, waitFor } from '@testing-library/svelte';
import { beforeEach, describe, expect, it, vi } from 'vitest';

const submitFeatureUsage = vi.hoisted(() => vi.fn(() => Promise.resolve()));
const toast = vi.hoisted(() => ({ success: vi.fn() }));
vi.mock('$features/auth/exceptionless-session', () => ({ submitFeatureUsage }));
vi.mock('svelte-sonner', () => ({ toast }));

import AssistantMessageActions from './assistant-message-actions.svelte';

describe('AssistantMessageActions', () => {
    const writeText = vi.fn(() => Promise.resolve());

    beforeEach(() => {
        writeText.mockClear();
        submitFeatureUsage.mockClear();
        toast.success.mockClear();
        Object.defineProperty(navigator, 'clipboard', { configurable: true, value: { writeText } });
    });

    it('copies the complete message and regenerates the response', async () => {
        const onRegenerate = vi.fn();
        render(AssistantMessageActions, { props: { content: 'The answer', onRegenerate } });

        await fireEvent.click(screen.getByRole('button', { name: 'Copy message' }));
        await fireEvent.click(screen.getByRole('button', { name: 'Regenerate response' }));

        expect(writeText).toHaveBeenCalledWith('The answer');
        expect(onRegenerate).toHaveBeenCalledOnce();
    });

    it('records helpful feedback without including message contents', async () => {
        const onFeedback = vi.fn();
        render(AssistantMessageActions, { props: { content: 'Sensitive answer', onFeedback, showFeedback: true } });

        await fireEvent.click(screen.getByRole('button', { name: 'Good response' }));

        expect(onFeedback).toHaveBeenCalledWith('helpful');
        await waitFor(() => expect(submitFeatureUsage).toHaveBeenCalledWith('assistant.ResponseHelpful'));
        expect(toast.success).toHaveBeenCalledWith('Marked as helpful.');
        expect(submitFeatureUsage).not.toHaveBeenCalledWith(expect.stringContaining('Sensitive answer'));
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
});
