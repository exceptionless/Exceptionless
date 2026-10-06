import type { SystemNotification } from '$features/websockets/models';

import { fireEvent, render, screen, waitFor } from '@testing-library/svelte';
import { beforeEach, describe, expect, it, vi } from 'vitest';

import SystemNotifications from './+page.svelte';

const mocks = vi.hoisted(() => ({
    clear: vi.fn(),
    notification: { data: null as null | SystemNotification, isLoading: false },
    save: vi.fn()
}));

vi.mock('$features/notifications/api.svelte', () => ({
    clearSystemNotificationMutation: () => ({ isPending: false, mutateAsync: mocks.clear }),
    getCurrentSystemNotificationQuery: () => mocks.notification,
    setSystemNotificationMutation: () => ({ isPending: false, mutateAsync: mocks.save })
}));

describe('system notifications after UI cutover', () => {
    beforeEach(() => {
        mocks.notification.data = null;
        mocks.save.mockReset().mockResolvedValue({});
        mocks.clear.mockReset().mockResolvedValue(undefined);
    });

    it('publishes new banners without offering a removed application target', async () => {
        render(SystemNotifications);

        expect(screen.getByRole('button', { name: 'Level' })).toBeInTheDocument();
        expect(screen.queryByText('Target')).not.toBeInTheDocument();
        expect(screen.queryByText(/Both UIs|Legacy UI only|New UI only/)).not.toBeInTheDocument();
        await fireEvent.input(screen.getByLabelText('Message'), { target: { value: 'Planned maintenance' } });
        await fireEvent.click(screen.getByRole('button', { name: 'Set Notification' }));

        await waitFor(() => expect(mocks.save).toHaveBeenCalledExactlyOnceWith({ level: 'Info', message: 'Planned maintenance', target: 'Both' }));
    });

    it.each(['Legacy', 'Modern', 'Both', undefined] as const)('keeps a saved %s banner unchanged until explicitly republished', async (target) => {
        const notification = { date: '2026-10-01T00:00:00Z', level: 'Warning' as const, message: 'Saved banner', target };
        mocks.notification.data = notification;
        render(SystemNotifications);

        await waitFor(() => expect(screen.getByLabelText('Message')).toHaveValue('Saved banner'));
        expect(mocks.save).not.toHaveBeenCalled();
        expect(mocks.clear).not.toHaveBeenCalled();
        expect(notification.target).toBe(target);
        if (target === 'Legacy') {
            expect(screen.getByText(/This saved notification is hidden/)).toBeInTheDocument();
        } else {
            expect(screen.queryByText(/This saved notification is hidden/)).not.toBeInTheDocument();
        }

        await fireEvent.click(screen.getByRole('button', { name: 'Update Notification' }));

        await waitFor(() => expect(mocks.save).toHaveBeenCalledExactlyOnceWith({ level: 'Warning', message: 'Saved banner', target: 'Both' }));
    });
});
