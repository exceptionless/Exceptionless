import { fireEvent, render, screen, waitFor } from '@testing-library/svelte';
import { beforeEach, describe, expect, it, vi } from 'vitest';

const state = vi.hoisted(() => ({
    enabled: false,
    error: vi.fn(),
    success: vi.fn(),
    update: vi.fn(),
    updateModel: vi.fn()
}));
vi.mock('svelte-sonner', () => ({ toast: { error: state.error, success: state.success } }));
vi.mock('$features/admin/api.svelte', () => ({
    getAdminAssistantSettingsQuery: () => ({
        data: {
            configured_enabled: true,
            configured_model: 'example/model',
            conversation_sharing_default_enabled: state.enabled,
            enabled: true,
            is_configured: true,
            is_enabled_overridden: false,
            is_overridden: false,
            model: 'example/model'
        },
        isError: false,
        isPending: false
    }),
    putAdminAssistantConversationSharingSettingsMutation: () => ({ isPending: false, mutateAsync: state.update }),
    putAdminAssistantEnabledSettingsMutation: () => ({ isPending: false, mutateAsync: vi.fn() }),
    putAdminAssistantSettingsMutation: () => ({ isPending: false, mutateAsync: state.updateModel })
}));

import AssistantSettings from './assistant-settings.svelte';

describe('Exie settings', () => {
    beforeEach(() => {
        state.enabled = false;
        state.update.mockReset();
        state.updateModel.mockReset();
        state.success.mockClear();
        state.error.mockClear();
        state.update.mockImplementation(async ({ enabled }: { enabled: boolean }) => ({ conversation_sharing_default_enabled: enabled }));
        state.updateModel.mockImplementation(async ({ model }: { model: string }) => ({ is_overridden: true, model }));
    });

    it('saves repeated model changes with Enter without inserting line breaks', async () => {
        render(AssistantSettings);
        const model = screen.getByRole('textbox', { name: 'Exie model' });
        await waitFor(() => expect(model).toHaveValue('example/model'));

        for (const name of ['openai/gpt-6.1-sol', 'anthropic/claude-sonnet-5.5']) {
            await fireEvent.input(model, { target: { value: name } });
            await fireEvent.keyDown(model, { key: 'Enter' });
            await waitFor(() => expect(state.updateModel).toHaveBeenLastCalledWith({ model: name }));
            await waitFor(() => expect(screen.getByRole('button', { name: 'Save Exie model' })).not.toBeDisabled());
            expect(model).toHaveValue(name);
        }

        expect(state.updateModel).toHaveBeenCalledTimes(2);
    });

    it('preserves single-line model IDs on paste and does not submit during composition', async () => {
        render(AssistantSettings);
        const model = screen.getByRole('textbox', { name: 'Exie model' });
        await waitFor(() => expect(model).toHaveValue('example/model'));
        await fireEvent.input(model, { target: { value: 'anthropic/claude-sonnet-5.5\r\n' } });
        await fireEvent.keyDown(model, { isComposing: true, key: 'Enter' });
        expect(state.updateModel).not.toHaveBeenCalled();
        expect(model).toHaveValue('anthropic/claude-sonnet-5.5');

        await fireEvent.click(screen.getByRole('button', { name: 'Save Exie model' }));
        await waitFor(() => expect(state.updateModel).toHaveBeenCalledWith({ model: 'anthropic/claude-sonnet-5.5' }));
    });

    it.each([false, true])('loads and saves the full logging switch from %s', async (enabled) => {
        state.enabled = enabled;
        render(AssistantSettings);
        const toggle = screen.getByRole('switch', { name: 'Conversation sharing default' });
        const save = screen.getByRole('button', { name: 'Save Exie conversation sharing default' });
        await waitFor(() => expect(toggle.getAttribute('aria-checked')).toBe(String(enabled)));
        expect(save.hasAttribute('disabled')).toBe(true);
        await fireEvent.click(toggle);
        await fireEvent.click(save);
        await waitFor(() => expect(state.update).toHaveBeenCalledWith({ enabled: !enabled }));
        await waitFor(() => expect(state.success).toHaveBeenCalledOnce());
    });

    it('shows a save failure without claiming the logging mode changed', async () => {
        state.update.mockRejectedValueOnce(new Error('Unavailable'));
        render(AssistantSettings);
        await fireEvent.click(screen.getByRole('switch', { name: 'Conversation sharing default' }));
        await fireEvent.click(screen.getByRole('button', { name: 'Save Exie conversation sharing default' }));
        await waitFor(() => expect(state.error).toHaveBeenCalledWith('Failed to update Exie conversation sharing default.'));
        expect(state.success).not.toHaveBeenCalled();
    });
});
