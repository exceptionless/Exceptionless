import { cleanup, fireEvent, render, screen } from '@testing-library/svelte';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';

const { query } = vi.hoisted(() => ({ query: { data: undefined as undefined | { date?: string; level: string; message: string; target?: string } } }));
vi.mock('$features/notifications/api.svelte', () => ({ getCurrentSystemNotificationQuery: () => query }));
vi.mock('$env/dynamic/public', () => ({ env: {} }));

import Notifications from './notifications.svelte';

describe('saved system notifications', () => {
    beforeEach(() => {
        localStorage.clear();
        query.data = undefined;
    });
    afterEach(cleanup);

    it.each([undefined, 'Both', 'Modern', 'Svelte'])('shows existing active audiences: %s', async (target) => {
        query.data = { level: 'Info', message: 'Service update', target };
        render(Notifications);
        expect(await screen.findByRole('alert')).toHaveTextContent('Service update');
    });

    it.each(['Legacy', 'Angular', 'Old UI'])('does not republish retired-site messages: %s', (target) => {
        query.data = { level: 'Info', message: 'Try the new site', target };
        render(Notifications);
        expect(screen.queryByRole('alert')).not.toBeInTheDocument();
    });

    it('keeps a saved notification dismissed across reloads and shows a changed realtime message', async () => {
        query.data = { date: '2026-01-01T00:00:00Z', level: 'Info', message: 'Service update', target: 'Modern' };
        const first = render(Notifications);
        await fireEvent.click(await screen.findByRole('button', { name: 'Dismiss alert' }));
        first.unmount();
        render(Notifications);
        expect(screen.queryByRole('alert')).not.toBeInTheDocument();

        document.dispatchEvent(new CustomEvent('SystemNotification', { detail: { level: 'Warning', message: 'New update', target: 'Both' } }));
        expect(await screen.findByRole('alert')).toHaveTextContent('New update');
    });
});
