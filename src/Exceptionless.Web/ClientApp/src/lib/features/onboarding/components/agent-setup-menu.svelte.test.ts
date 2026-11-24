import { cleanup, fireEvent, render, screen, waitFor } from '@testing-library/svelte';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';

const toast = vi.hoisted(() => ({ error: vi.fn(), success: vi.fn() }));
vi.mock('svelte-sonner', () => ({ toast }));

import { buildAgentSetupPrompt } from '../agent-setup';
import { findPlatform } from '../platforms';
import AgentSetupMenu from './agent-setup-menu.svelte';

const context = { apiKey: 'API_KEY_VALUE', submissionServerUrl: 'https://ex.example.com' };

describe('AgentSetupMenu', () => {
    const writeText = vi.fn(() => Promise.resolve());

    beforeEach(() => {
        writeText.mockClear();
        toast.success.mockClear();
        Object.defineProperty(navigator, 'clipboard', { configurable: true, value: { writeText } });
    });

    afterEach(async () => {
        cleanup();
        // Bits UI defers body-scroll restoration by 24 ms after an overlay unmounts.
        await new Promise((resolve) => window.setTimeout(resolve, 30));
    });

    it("copies the chosen platform's setup prompt", async () => {
        // Arrange
        const platform = findPlatform('javascript-nodejs')!;
        render(AgentSetupMenu, { props: { context, platform } });

        // Act
        await fireEvent.click(screen.getByRole('button', { name: 'Set up with your agent' }));
        await fireEvent.click(screen.getByRole('menuitem', { name: /Copy setup prompt/ }));

        // Assert
        await waitFor(() => expect(writeText).toHaveBeenCalledWith(buildAgentSetupPrompt(context, platform)));
        expect(toast.success).toHaveBeenCalledWith('Setup prompt copied. Paste it into your coding agent.');
    });

    it('opens each agent in a new tab with the prompt filled in', async () => {
        // Arrange
        render(AgentSetupMenu, { props: { context } });
        const encoded = encodeURIComponent(buildAgentSetupPrompt(context));

        // Act
        await fireEvent.click(screen.getByRole('button', { name: 'Set up with your agent' }));

        // Assert
        const links = screen.getAllByRole('menuitem').filter((item) => item.tagName === 'A') as HTMLAnchorElement[];
        expect(links.map((link) => [link.textContent?.replace(/\s+/g, ' ').trim(), link.getAttribute('href'), link.target])).toEqual([
            ['Cursor (opens in a new tab)', `https://cursor.com/link/prompt?text=${encoded}`, '_blank'],
            ['Claude (opens in a new tab)', `https://claude.ai/new?q=${encoded}`, '_blank'],
            ['ChatGPT (opens in a new tab)', `https://chatgpt.com/?q=${encoded}`, '_blank']
        ]);
    });
});
