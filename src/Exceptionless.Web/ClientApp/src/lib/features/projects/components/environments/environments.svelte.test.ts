import { cleanup, render, screen, waitFor } from '@testing-library/svelte';
import userEvent from '@testing-library/user-event';
import { afterEach, describe, expect, it, vi } from 'vitest';

import AddEnvironmentForm from './add-environment-form.svelte';
import DiscardedEnvironmentsTable from './discarded-environments-table.svelte';
import EnvironmentSettings from './environment-settings.svelte';
import EnvironmentsTable from './environments-table.svelte';

describe('EnvironmentsTable', () => {
    afterEach(() => cleanup());

    const rows = [
        { count: 120, isListed: true, key: 'production', lastSeen: '2026-11-02T10:00:00Z', name: 'Production', notify: true },
        { count: 0, isListed: true, key: 'dev', name: 'dev', notify: false },
        { count: 3, isListed: false, key: 'prodution', lastSeen: '2026-11-02T09:00:00Z', name: 'prodution' }
    ];

    it('shows listed names in their stored casing and offers Add only for unlisted names', async () => {
        // Arrange
        const add = vi.fn();
        const remove = vi.fn();
        render(EnvironmentsTable, { add, notifyOtherEnvironments: false, remove, rows, toggleNotify: vi.fn() });

        // Act
        await userEvent.click(screen.getByRole('button', { name: 'Add prodution' }));
        await userEvent.click(screen.getByRole('button', { name: 'Remove Production' }));

        // Assert
        expect(screen.getByText('Production')).toBeTruthy();
        expect(screen.queryByRole('button', { name: 'Add Production' })).toBeNull();
        expect(screen.queryByRole('button', { name: 'Remove prodution' })).toBeNull();
        expect(screen.getByText('Off (other environments)')).toBeTruthy();
        expect(add).toHaveBeenCalledWith(rows[2]);
        expect(remove).toHaveBeenCalledWith(rows[0]);
    });

    it('toggles notifications for a listed environment', async () => {
        // Arrange
        const toggleNotify = vi.fn();
        render(EnvironmentsTable, { add: vi.fn(), notifyOtherEnvironments: true, remove: vi.fn(), rows, toggleNotify });

        // Act
        await userEvent.click(screen.getByRole('switch', { name: 'Send notifications for dev' }));

        // Assert
        expect(toggleNotify).toHaveBeenCalledWith(rows[1], true);
    });
});

describe('DiscardedEnvironmentsTable', () => {
    afterEach(() => cleanup());

    it('shows counts and lets the owner allow a name or stop discarding events without an environment', async () => {
        // Arrange
        const allow = vi.fn();
        const stopDiscardingUnspecified = vi.fn();
        const named = [{ count: 3412, key: 'prodution', last_seen_utc: '2026-11-02T09:00:00Z', name: 'Prodution' }];
        const unspecified = { count: 8, last_seen_utc: '2026-11-02T09:00:00Z' };
        render(DiscardedEnvironmentsTable, { allow, named, retentionDays: 7, stopDiscardingUnspecified, unspecified });

        // Act
        await userEvent.click(screen.getByRole('button', { name: 'Allow Prodution' }));
        await userEvent.click(screen.getByRole('button', { name: 'Stop discarding events without an environment' }));

        // Assert
        expect(screen.getByText((3412).toLocaleString())).toBeTruthy();
        expect(screen.getByText('Events discarded (7 days)')).toBeTruthy();
        expect(allow).toHaveBeenCalledWith(named[0]);
        expect(stopDiscardingUnspecified).toHaveBeenCalledOnce();
    });

    it('reports events from names that were too many to list', () => {
        // Act
        render(DiscardedEnvironmentsTable, {
            allow: vi.fn(),
            named: [{ count: 5, key: 'dev', last_seen_utc: '2026-11-02T09:00:00Z', name: 'dev' }],
            otherEvents: 1200,
            otherNames: 100,
            otherNamesCapped: true,
            retentionDays: 7,
            stopDiscardingUnspecified: vi.fn()
        });

        // Assert
        expect(screen.getByText(/1,200 events from at least 100 other names were discarded/)).toBeTruthy();
        expect(screen.getByText(/cannot be allowed here\. Check the environment your clients send, add the names you expect to the list above/)).toBeTruthy();
    });

    it('explains when nothing has been discarded', () => {
        // Act
        render(DiscardedEnvironmentsTable, { allow: vi.fn(), named: [], retentionDays: 7, stopDiscardingUnspecified: vi.fn() });

        // Assert
        expect(screen.getByText(/No events have been discarded because of their environment in the last 7 days/)).toBeTruthy();
    });
});

describe('EnvironmentSettings', () => {
    afterEach(() => cleanup());

    it('only allows discarding events without an environment while unlisted environments are discarded', async () => {
        // Arrange
        const update = vi.fn();
        render(EnvironmentSettings, {
            discardEventsWithoutEnvironment: false,
            discardUnlistedEnvironments: false,
            notifyOtherEnvironments: true,
            update
        });
        const withoutEnvironment = screen.getByRole('switch', { name: 'Also discard events that have no environment' });

        // Act
        await userEvent.click(withoutEnvironment);
        await userEvent.click(screen.getByRole('switch', { name: 'Discard events from environments that are not listed' }));
        await userEvent.click(screen.getByRole('switch', { name: 'Send notifications for other environments' }));

        // Assert
        expect(withoutEnvironment.hasAttribute('disabled')).toBe(true);
        expect(update).toHaveBeenNthCalledWith(1, { discard_unlisted_environments: true });
        expect(update).toHaveBeenNthCalledWith(2, { notify_other_environments: false });
        expect(update).toHaveBeenCalledTimes(2);
    });
});

describe('AddEnvironmentForm', () => {
    afterEach(() => cleanup());

    it('adds the trimmed name with the chosen notification setting', async () => {
        // Arrange
        const add = vi.fn().mockResolvedValue(undefined);
        render(AddEnvironmentForm, { add, listedNames: ['Production'] });

        // Act
        await userEvent.type(screen.getByLabelText('Environment name'), '  Staging West ');
        await userEvent.click(screen.getByRole('switch', { name: 'Send notifications' }));
        await userEvent.click(screen.getByRole('button', { name: 'Add environment' }));

        // Assert
        await waitFor(() => expect(add).toHaveBeenCalledWith({ name: 'Staging West', notify: false }));
    });

    it.each([
        ['production', 'production is already listed'],
        ['  PRODUCTION  ', 'PRODUCTION is already listed']
    ])('rejects %j because names are not case-sensitive', async (name, message) => {
        // Arrange
        const add = vi.fn();
        render(AddEnvironmentForm, { add, listedNames: ['Production'] });

        // Act
        await userEvent.type(screen.getByLabelText('Environment name'), name);
        await userEvent.click(screen.getByRole('button', { name: 'Add environment' }));

        // Assert
        expect(await screen.findByText(new RegExp(message))).toBeTruthy();
        expect(add).not.toHaveBeenCalled();
    });

    it('fills in the name it is given so the casing can be confirmed before adding', async () => {
        // Arrange
        const add = vi.fn().mockResolvedValue(undefined);
        render(AddEnvironmentForm, { add, listedNames: [], prefill: { name: 'qa-west' } });
        const input = screen.getByLabelText('Environment name') as HTMLInputElement;
        await waitFor(() => expect(input.value).toBe('qa-west'));

        // Act
        await userEvent.clear(input);
        await userEvent.type(input, 'QA-West');
        await userEvent.click(screen.getByRole('button', { name: 'Add environment' }));

        // Assert
        await waitFor(() => expect(add).toHaveBeenCalledWith({ name: 'QA-West', notify: true }));
    });

    it('rejects a name that is only whitespace', async () => {
        // Arrange
        const add = vi.fn();
        render(AddEnvironmentForm, { add, listedNames: [] });

        // Act
        await userEvent.type(screen.getByLabelText('Environment name'), '   ');
        await userEvent.click(screen.getByRole('button', { name: 'Add environment' }));

        // Assert
        expect(await screen.findByText(/Name must be 1 to 64 characters/)).toBeTruthy();
        expect(add).not.toHaveBeenCalled();
    });

    it('requires a name', async () => {
        // Arrange
        const add = vi.fn();
        render(AddEnvironmentForm, { add, listedNames: [] });

        // Act
        await userEvent.click(screen.getByRole('button', { name: 'Add environment' }));

        // Assert
        expect(await screen.findByText('Name is required')).toBeTruthy();
        expect(add).not.toHaveBeenCalled();
    });
});
