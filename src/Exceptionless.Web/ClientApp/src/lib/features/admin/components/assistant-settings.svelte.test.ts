import { fireEvent, render, screen, waitFor } from '@testing-library/svelte';
import { beforeEach, describe, expect, it, vi } from 'vitest';

const state = vi.hoisted(() => ({
    enabled: false,
    error: vi.fn(),
    success: vi.fn(),
    update: vi.fn()
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
    putAdminAssistantSettingsMutation: () => ({ isPending: false, mutateAsync: vi.fn() })
}));

import AssistantSettings from './assistant-settings.svelte';

describe('Exie conversation sharing default settings', () => {
    beforeEach(() => {
        state.enabled = false;
        state.update.mockReset();
        state.success.mockClear();
        state.error.mockClear();
        state.update.mockImplementation(async ({ enabled }: { enabled: boolean }) => ({ conversation_sharing_default_enabled: enabled }));
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
