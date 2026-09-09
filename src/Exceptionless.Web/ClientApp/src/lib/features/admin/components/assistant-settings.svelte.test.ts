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
            enabled: true,
            full_logging_enabled: state.enabled,
            is_configured: true,
            is_enabled_overridden: false,
            is_overridden: false,
            model: 'example/model'
        },
        isError: false,
        isPending: false
    }),
    putAdminAssistantEnabledSettingsMutation: () => ({ isPending: false, mutateAsync: vi.fn() }),
    putAdminAssistantFullLoggingSettingsMutation: () => ({ isPending: false, mutateAsync: state.update }),
    putAdminAssistantSettingsMutation: () => ({ isPending: false, mutateAsync: vi.fn() })
}));

import AssistantSettings from './assistant-settings.svelte';

describe('Exie full logging settings', () => {
    beforeEach(() => {
        state.enabled = false;
        state.update.mockReset();
        state.success.mockClear();
        state.error.mockClear();
        state.update.mockImplementation(async ({ enabled }: { enabled: boolean }) => ({ full_logging_enabled: enabled }));
    });

    it.each([false, true])('loads and saves the full logging switch from %s', async (enabled) => {
        state.enabled = enabled;
        render(AssistantSettings);
        const toggle = screen.getByRole('switch', { name: 'Full logging' });
        const save = screen.getByRole('button', { name: 'Save Exie full logging' });
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
        await fireEvent.click(screen.getByRole('switch', { name: 'Full logging' }));
        await fireEvent.click(screen.getByRole('button', { name: 'Save Exie full logging' }));
        await waitFor(() => expect(state.error).toHaveBeenCalledWith('Failed to update Exie full logging.'));
        expect(state.success).not.toHaveBeenCalled();
    });
});
