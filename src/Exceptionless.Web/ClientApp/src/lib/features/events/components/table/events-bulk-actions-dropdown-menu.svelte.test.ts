import { cleanup, fireEvent, render, screen, waitFor } from '@testing-library/svelte';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';

const mutateAsync = vi.hoisted(() => vi.fn());
const deleteEvent = vi.hoisted(() => vi.fn(() => ({ mutateAsync })));
const toast = vi.hoisted(() => ({ error: vi.fn(), success: vi.fn() }));
const startBulkOperation = vi.hoisted(() => vi.fn());
const postBulkOperation = vi.hoisted(() => vi.fn(() => ({ mutateAsync: startBulkOperation })));

vi.mock('#features/events/api.svelte.ts', () => ({ deleteEvent }));
vi.mock('#features/bulk-operations/api.svelte.ts', () => ({ postBulkOperation }));
vi.mock('svelte-sonner', () => ({ toast }));

import EventsBulkActionsDropdownMenu from './events-bulk-actions-dropdown-menu.svelte';

describe('EventsBulkActionsDropdownMenu', () => {
    beforeEach(() => {
        mutateAsync.mockResolvedValue(undefined);
        startBulkOperation.mockReset();
        startBulkOperation.mockResolvedValue(undefined);
        deleteEvent.mockClear();
        toast.success.mockClear();
        toast.error.mockClear();
    });

    afterEach(async () => {
        cleanup();
        // Bits UI defers body-scroll restoration by 24 ms after an overlay unmounts.
        await new Promise((resolve) => window.setTimeout(resolve, 30));
    });

    it('requires a selected event before opening bulk actions', async () => {
        const table = {
            getSelectedRowModel: () => ({ flatRows: [] }),
            resetRowSelection: vi.fn()
        } as never;
        render(EventsBulkActionsDropdownMenu, { props: { table } });

        const trigger = screen.getByRole('button', { name: 'Actions' }) as HTMLButtonElement;
        expect(trigger.disabled).toBe(false);
        expect(trigger.getAttribute('aria-disabled')).toBe('true');
        expect(trigger.title).toBe('Select one or more events to use bulk actions');
        expect(document.getElementById(trigger.getAttribute('aria-describedby')!)?.textContent).toBe('Select one or more events to use bulk actions.');
        trigger.focus();
        expect(document.activeElement).toBe(trigger);
        await fireEvent.click(trigger);
        expect(screen.queryByRole('menuitem', { name: 'Delete' })).toBeNull();
    });

    it('does not repeat the trigger label inside the menu', async () => {
        const table = {
            getSelectedRowModel: () => ({ flatRows: [{ id: 'event-id' }] }),
            resetRowSelection: vi.fn()
        } as never;
        render(EventsBulkActionsDropdownMenu, { props: { table } });

        await fireEvent.click(screen.getByRole('button', { name: 'Actions' }));

        expect(document.querySelector('[data-slot="dropdown-menu-group-heading"]')).toBeNull();
        expect(screen.getByRole('menuitem', { name: 'Delete' })).toBeTruthy();
    });

    it('deletes the selected events and clears the selection', async () => {
        // Arrange
        const resetRowSelection = vi.fn();
        const table = {
            getSelectedRowModel: () => ({ flatRows: [{ id: 'event-id' }] }),
            resetRowSelection
        } as never;
        render(EventsBulkActionsDropdownMenu, { props: { table } });

        // Act
        await fireEvent.click(screen.getByRole('button', { name: 'Actions' }));
        await fireEvent.click(screen.getByRole('menuitem', { name: 'Delete' }));
        await fireEvent.click(screen.getByRole('button', { name: 'Delete Event' }));

        // Assert
        await waitFor(() => expect(mutateAsync).toHaveBeenCalledOnce());
        expect(resetRowSelection).toHaveBeenCalledOnce();
        expect(toast.success).toHaveBeenCalledWith('Successfully deleted event.');
        expect(startBulkOperation).not.toHaveBeenCalled();
    });

    it('uses the selected count for the bulk delete confirmation', async () => {
        // Arrange
        const table = {
            getSelectedRowModel: () => ({ flatRows: [{ id: 'event-id-1' }, { id: 'event-id-2' }] }),
            resetRowSelection: vi.fn()
        } as never;
        render(EventsBulkActionsDropdownMenu, { props: { table } });

        // Act
        await fireEvent.click(screen.getByRole('button', { name: 'Actions' }));
        await fireEvent.click(screen.getByRole('menuitem', { name: 'Delete' }));
        await fireEvent.click(screen.getByRole('button', { name: 'Delete 2 Events' }));

        // Assert
        await waitFor(() => expect(mutateAsync).toHaveBeenCalledOnce());
        expect(toast.success).toHaveBeenCalledWith('Successfully deleted 2 events.');
    });

    describe('when everything matching the filter is selected', () => {
        const allMatching = { count: 1234, filter: 'type:error', offset: '-300m', organizationId: 'organization-id', time: '[now-7d TO now]' };

        it('enables the menu without selected rows and confirms the exact count', async () => {
            // Arrange
            const table = { getSelectedRowModel: () => ({ flatRows: [] }), resetRowSelection: vi.fn() } as never;
            render(EventsBulkActionsDropdownMenu, { props: { allMatching, table } });

            // Act
            const trigger = screen.getByRole('button', { name: 'Actions' });
            await fireEvent.click(trigger);
            await fireEvent.click(screen.getByRole('menuitem', { name: 'Delete' }));

            // Assert
            expect(trigger.getAttribute('aria-disabled')).toBe('false');
            expect(screen.getByText(/Delete\s+1,234\s+events matching this filter\? This can't be undone\./)).toBeTruthy();
            expect(screen.getByRole('button', { name: 'Delete 1,234 Events' })).toBeTruthy();
        });

        it('deletes against the filter instead of the selected rows', async () => {
            // Arrange
            const resetRowSelection = vi.fn();
            const onAllMatchingStarted = vi.fn();
            const table = { getSelectedRowModel: () => ({ flatRows: [{ id: 'event-id' }] }), resetRowSelection } as never;
            render(EventsBulkActionsDropdownMenu, { props: { allMatching, onAllMatchingStarted, table } });

            // Act
            await fireEvent.click(screen.getByRole('button', { name: 'Actions' }));
            await fireEvent.click(screen.getByRole('menuitem', { name: 'Delete' }));
            await fireEvent.click(screen.getByRole('button', { name: 'Delete 1,234 Events' }));

            // Assert
            await waitFor(() => expect(startBulkOperation).toHaveBeenCalledOnce());
            expect(startBulkOperation).toHaveBeenCalledWith({
                organization_id: 'organization-id',
                selection: { filter: 'type:error', kind: 'Filter', offset: '-300m', time: '[now-7d TO now]' },
                type: 'events.delete'
            });
            expect(mutateAsync).not.toHaveBeenCalled();
            expect(resetRowSelection).toHaveBeenCalledOnce();
            expect(onAllMatchingStarted).toHaveBeenCalledOnce();
            expect(toast.success).toHaveBeenCalledWith(expect.stringContaining('Deleting 1,234 events'));
        });

        it('keeps the selection when starting the operation fails', async () => {
            // Arrange
            startBulkOperation.mockRejectedValue({ title: 'Another destructive operation is already running for this project.' });
            const resetRowSelection = vi.fn();
            const onAllMatchingStarted = vi.fn();
            const table = { getSelectedRowModel: () => ({ flatRows: [] }), resetRowSelection } as never;
            render(EventsBulkActionsDropdownMenu, { props: { allMatching, onAllMatchingStarted, table } });

            // Act
            await fireEvent.click(screen.getByRole('button', { name: 'Actions' }));
            await fireEvent.click(screen.getByRole('menuitem', { name: 'Delete' }));
            await fireEvent.click(screen.getByRole('button', { name: 'Delete 1,234 Events' }));

            // Assert
            await waitFor(() => expect(startBulkOperation).toHaveBeenCalledOnce());
            expect(resetRowSelection).not.toHaveBeenCalled();
            expect(onAllMatchingStarted).not.toHaveBeenCalled();
            expect(toast.success).not.toHaveBeenCalled();
            expect(toast.error).toHaveBeenCalledWith('Another destructive operation is already running for this project.');
        });
    });
});
