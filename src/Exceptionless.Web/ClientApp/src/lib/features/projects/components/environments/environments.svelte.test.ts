import { cleanup, render, screen, waitFor } from '@testing-library/svelte';
import userEvent from '@testing-library/user-event';
import { afterEach, describe, expect, it, vi } from 'vitest';

import AddEnvironmentDialog from './add-environment-dialog.svelte';
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

    const tableProps = {
        add: vi.fn(),
        discardUnlisted: false,
        notifyOtherEnvironments: true,
        remove: vi.fn(),
        rows,
        toggleNotify: vi.fn(),
        toggleNotifyOther: vi.fn()
    };

    it('shows listed names in their stored casing and offers Add only for unlisted names', async () => {
        // Arrange
        const add = vi.fn();
        const remove = vi.fn();
        render(EnvironmentsTable, { ...tableProps, add, remove });

        // Act
        await userEvent.click(screen.getByRole('button', { name: 'Add prodution' }));
        await userEvent.click(screen.getByRole('button', { name: 'Remove Production' }));

        // Assert
        expect(screen.getByText('Production')).toBeTruthy();
        expect(screen.queryByRole('button', { name: 'Add Production' })).toBeNull();
        expect(screen.queryByRole('button', { name: 'Remove prodution' })).toBeNull();
        expect(screen.getByText('Not added')).toBeTruthy();
        expect(screen.getByText('Default')).toBeTruthy();
        expect(add).toHaveBeenCalledWith(rows[2]);
        expect(remove).toHaveBeenCalledWith(rows[0]);
    });

    it('toggles notifications for a listed environment', async () => {
        // Arrange
        const toggleNotify = vi.fn();
        render(EnvironmentsTable, { ...tableProps, toggleNotify });

        // Act
        await userEvent.click(screen.getByRole('switch', { name: 'Send notifications for dev' }));

        // Assert
        expect(toggleNotify).toHaveBeenCalledWith(rows[1], true);
    });

    it('toggles notifications for environments that are not in the list', async () => {
        // Arrange
        const toggleNotifyOther = vi.fn();
        render(EnvironmentsTable, { ...tableProps, toggleNotifyOther });

        // Act
        await userEvent.click(screen.getByRole('switch', { name: 'Send notifications for other environments' }));

        // Assert
        expect(toggleNotifyOther).toHaveBeenCalledWith(false);
    });

    it('marks unlisted environments as discarded while only listed environments are accepted', () => {
        // Act
        render(EnvironmentsTable, { ...tableProps, discardUnlisted: true });

        // Assert
        expect(screen.getByText('Discarded')).toBeTruthy();
        expect(screen.queryByText('Not added')).toBeNull();
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
        expect(
            screen.getByText(/Plus 1,200 events from at least 100 more names, too many to list here\. Check the environment your clients send\./)
        ).toBeTruthy();
    });

    it('explains when nothing has been discarded', () => {
        // Act
        render(DiscardedEnvironmentsTable, { allow: vi.fn(), named: [], retentionDays: 7, stopDiscardingUnspecified: vi.fn() });

        // Assert
        expect(screen.getByText('Nothing discarded in the last 7 days.')).toBeTruthy();
    });
});

describe('EnvironmentSettings', () => {
    afterEach(() => cleanup());

    it('switches between accepting any environment and only listed ones', async () => {
        // Arrange
        const update = vi.fn();
        render(EnvironmentSettings, { discardEventsWithoutEnvironment: false, discardUnlistedEnvironments: false, update });

        // Act
        await userEvent.click(screen.getByRole('radio', { name: 'Only environments in this list' }));

        // Assert
        expect(update).toHaveBeenCalledWith({ discard_unlisted_environments: true });
        expect(screen.queryByRole('checkbox', { name: "Also accept events that don't send an environment" })).toBeNull();
    });

    it('offers to accept events without an environment only while unlisted environments are discarded', async () => {
        // Arrange
        const update = vi.fn();
        render(EnvironmentSettings, { discardEventsWithoutEnvironment: false, discardUnlistedEnvironments: true, update });
        const acceptWithout = screen.getByRole('checkbox', { name: "Also accept events that don't send an environment" });
        const initiallyChecked = acceptWithout.getAttribute('aria-checked');

        // Act
        await userEvent.click(acceptWithout);

        // Assert
        expect(initiallyChecked).toBe('true');
        expect(update).toHaveBeenCalledWith({ discard_events_without_environment: true });
    });
});

describe('AddEnvironmentDialog', () => {
    afterEach(() => cleanup());

    async function submit(name?: string) {
        const input = screen.getByLabelText('Name') as HTMLInputElement;
        if (name !== undefined) {
            await userEvent.clear(input);
            if (name) {
                await userEvent.type(input, name);
            }
        }

        await userEvent.click(screen.getByRole('button', { name: 'Add Environment' }));
    }

    it('adds the trimmed name', async () => {
        // Arrange
        const add = vi.fn().mockResolvedValue(undefined);
        render(AddEnvironmentDialog, { add, listedNames: ['Production'], open: true });

        // Act
        await submit('  Staging West ');

        // Assert
        await waitFor(() => expect(add).toHaveBeenCalledWith({ name: 'Staging West', notify: true }));
    });

    it.each([
        ['production', 'production is already in the list'],
        ['  PRODUCTION  ', 'PRODUCTION is already in the list']
    ])('rejects %j because names are not case-sensitive', async (name, message) => {
        // Arrange
        const add = vi.fn();
        render(AddEnvironmentDialog, { add, listedNames: ['Production'], open: true });

        // Act
        await submit(name);

        // Assert
        expect(await screen.findByText(new RegExp(message))).toBeTruthy();
        expect(add).not.toHaveBeenCalled();
    });

    it('fills in the name it is given so the casing can be corrected before adding', async () => {
        // Arrange
        const add = vi.fn().mockResolvedValue(undefined);
        render(AddEnvironmentDialog, { add, listedNames: [], name: 'qa-west', open: true });
        const input = screen.getByLabelText('Name') as HTMLInputElement;
        await waitFor(() => expect(input.value).toBe('qa-west'));

        // Act
        await submit('QA-West');

        // Assert
        await waitFor(() => expect(add).toHaveBeenCalledWith({ name: 'QA-West', notify: true }));
    });

    it('rejects a name that is only whitespace', async () => {
        // Arrange
        const add = vi.fn();
        render(AddEnvironmentDialog, { add, listedNames: [], open: true });

        // Act
        await submit('   ');

        // Assert
        expect(await screen.findByText(/Use 1 to 64 characters/)).toBeTruthy();
        expect(add).not.toHaveBeenCalled();
    });

    it('requires a name', async () => {
        // Arrange
        const add = vi.fn();
        render(AddEnvironmentDialog, { add, listedNames: [], open: true });

        // Act
        await submit();

        // Assert
        expect(await screen.findByText('Name is required')).toBeTruthy();
        expect(add).not.toHaveBeenCalled();
    });
});
