import { fireEvent, render, screen, waitFor } from '@testing-library/svelte';
import { beforeEach, describe, expect, it, vi } from 'vitest';

const mutateAsync = vi.hoisted(() => vi.fn());
const deleteEvent = vi.hoisted(() => vi.fn(() => ({ mutateAsync })));
const toast = vi.hoisted(() => ({ success: vi.fn() }));

vi.mock('$features/events/api.svelte', () => ({ deleteEvent }));
vi.mock('svelte-sonner', () => ({ toast }));

import EventsBulkActionsDropdownMenu from './events-bulk-actions-dropdown-menu.svelte';

describe('EventsBulkActionsDropdownMenu', () => {
    beforeEach(() => {
        mutateAsync.mockResolvedValue(undefined);
        deleteEvent.mockClear();
        toast.success.mockClear();
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
        await fireEvent.click(screen.getByRole('button', { name: /Bulk Actions/ }));
        await fireEvent.click(screen.getByRole('menuitem', { name: 'Delete' }));
        await fireEvent.click(screen.getByRole('button', { name: 'Delete Event' }));

        // Assert
        await waitFor(() => expect(mutateAsync).toHaveBeenCalledOnce());
        expect(resetRowSelection).toHaveBeenCalledOnce();
        expect(toast.success).toHaveBeenCalledWith('Successfully deleted event.');
    });

    it('uses the selected count for the bulk delete confirmation', async () => {
        // Arrange
        const table = {
            getSelectedRowModel: () => ({ flatRows: [{ id: 'event-id-1' }, { id: 'event-id-2' }] }),
            resetRowSelection: vi.fn()
        } as never;
        render(EventsBulkActionsDropdownMenu, { props: { table } });

        // Act
        await fireEvent.click(screen.getByRole('button', { name: /Bulk Actions/ }));
        await fireEvent.click(screen.getByRole('menuitem', { name: 'Delete' }));
        await fireEvent.click(screen.getByRole('button', { name: 'Delete 2 Events' }));

        // Assert
        await waitFor(() => expect(mutateAsync).toHaveBeenCalledOnce());
        expect(toast.success).toHaveBeenCalledWith('Successfully deleted 2 events.');
    });
});
