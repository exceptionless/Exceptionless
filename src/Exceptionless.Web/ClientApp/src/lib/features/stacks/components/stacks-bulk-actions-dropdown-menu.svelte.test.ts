import { cleanup, fireEvent, render, screen, waitFor } from '@testing-library/svelte';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';

const startBulk = vi.hoisted(() => vi.fn());
const toast = vi.hoisted(() => ({ error: vi.fn(), success: vi.fn() }));
const mutations = vi.hoisted(() => ({ changeStatus: vi.fn(), delete: vi.fn(), markFixed: vi.fn(), snooze: vi.fn() }));

vi.mock('svelte-sonner', () => ({ toast }));
vi.mock('#features/tasks/api.svelte.ts', () => ({ postBulkOperation: () => ({ mutateAsync: startBulk }) }));

vi.mock('../api.svelte', () => ({
    deleteStack: vi.fn(() => ({ mutateAsync: mutations.delete })),
    postChangeStatus: vi.fn(() => ({ mutateAsync: mutations.changeStatus })),
    postMarkFixed: vi.fn(() => ({ mutateAsync: mutations.markFixed })),
    postMarkSnoozed: vi.fn(() => ({ mutateAsync: mutations.snooze }))
}));

import { BulkOperationSelectionKind } from '#features/tasks/models.ts';

import StacksBulkActionsDropdownMenu from './stacks-bulk-actions-dropdown-menu.svelte';

const allMatching = { count: 1234, filter: 'status:open', offset: '60m', organizationId: 'organization-id', time: '[now-7d TO now]' };
const selection = { filter: 'status:open', kind: BulkOperationSelectionKind.Filter, offset: '60m', time: '[now-7d TO now]' };

async function choose(item: string) {
    await fireEvent.click(screen.getByRole('button', { name: 'Actions' }));
    await fireEvent.click(screen.getByRole('menuitem', { name: item }));
}

function renderMatching(onAllMatchingStarted = vi.fn()) {
    const resetRowSelection = vi.fn();
    const table = { getSelectedRowModel: () => ({ flatRows: [{ id: 'a' }] }), resetRowSelection } as never;
    render(StacksBulkActionsDropdownMenu, { props: { allMatching, onAllMatchingStarted, table } });
    return { onAllMatchingStarted, resetRowSelection };
}

describe('StacksBulkActionsDropdownMenu', () => {
    beforeEach(() => {
        startBulk.mockReset();
        startBulk.mockResolvedValue(undefined);
        toast.error.mockClear();
        toast.success.mockClear();
        Object.values(mutations).forEach((m) => m.mockReset());
    });

    afterEach(async () => {
        cleanup();
        // Bits UI defers body-scroll restoration by 24 ms after an overlay unmounts.
        await new Promise((resolve) => window.setTimeout(resolve, 30));
    });

    it('requires a selected stack before opening bulk actions', async () => {
        const table = {
            getSelectedRowModel: () => ({ flatRows: [] }),
            resetRowSelection: vi.fn()
        } as never;

        render(StacksBulkActionsDropdownMenu, { props: { table } });

        const trigger = screen.getByRole('button', { name: 'Actions' }) as HTMLButtonElement;
        expect(trigger.disabled).toBe(false);
        expect(trigger.getAttribute('aria-disabled')).toBe('true');
        expect(trigger.title).toBe('Select one or more stacks to use bulk actions');
        expect(document.getElementById(trigger.getAttribute('aria-describedby')!)?.textContent).toBe('Select one or more stacks to use bulk actions.');
        trigger.focus();
        expect(document.activeElement).toBe(trigger);
        await fireEvent.click(trigger);
        expect(screen.queryByRole('menuitem', { name: 'Mark Open' })).toBeNull();
    });

    it('enables bulk actions when a stack is selected', () => {
        const table = {
            getSelectedRowModel: () => ({ flatRows: [{ id: 'stack-id' }] }),
            resetRowSelection: vi.fn()
        } as never;

        render(StacksBulkActionsDropdownMenu, { props: { table } });

        const trigger = screen.getByRole('button', { name: 'Actions' }) as HTMLButtonElement;
        expect(trigger.disabled).toBe(false);
        expect(trigger.title).toBe('Actions');
    });

    it('changes the selected stacks through the per-id endpoint when all matching is not active', async () => {
        // Arrange
        const table = { getSelectedRowModel: () => ({ flatRows: [{ id: 'a' }] }), resetRowSelection: vi.fn() } as never;
        render(StacksBulkActionsDropdownMenu, { props: { table } });

        // Act
        await choose('Mark Open');

        // Assert
        await waitFor(() => expect(mutations.changeStatus).toHaveBeenCalledWith('open'));
        expect(startBulk).not.toHaveBeenCalled();
    });

    it.each([
        ['Mark Open', 'Mark Matching Stacks Open', 'stacks.change-status', { status: 'open' }],
        ['Mark Ignored', 'Mark Matching Stacks Ignored', 'stacks.change-status', { status: 'ignored' }]
    ])('confirms the estimated count and then starts %s on everything matching', async (item, button, type, parameters) => {
        // Arrange
        const { onAllMatchingStarted, resetRowSelection } = renderMatching();

        // Act
        await choose(item);
        const dialogText = screen.getByRole('alertdialog').textContent?.replace(/\s+/g, ' ');
        await fireEvent.click(screen.getByRole('button', { name: button }));

        // Assert
        expect(dialogText).toContain('About 1,234 stacks match this filter. The final count may differ.');
        await waitFor(() => expect(startBulk).toHaveBeenCalledWith({ organization_id: 'organization-id', parameters, selection, type }));
        expect(onAllMatchingStarted).toHaveBeenCalledOnce();
        expect(resetRowSelection).toHaveBeenCalledOnce();
        expect(mutations.changeStatus).not.toHaveBeenCalled();
    });

    it('only opens the snooze durations when the submenu is opened with the keyboard', async () => {
        // Arrange
        renderMatching();
        await fireEvent.click(screen.getByRole('button', { name: 'Actions' }));
        const trigger = screen.getByRole('menuitem', { name: 'Mark Snoozed' });
        trigger.focus();

        // Act
        await fireEvent.keyDown(trigger, { key: 'ArrowRight' });

        // Assert
        expect(await screen.findByRole('menuitem', { name: '6 Hours' })).toBeTruthy();
        expect(screen.queryByRole('alertdialog')).toBeNull();
        expect(startBulk).not.toHaveBeenCalled();
    });

    it('confirms the chosen snooze duration and its end time before snoozing everything matching', async () => {
        // Arrange
        renderMatching();
        await fireEvent.click(screen.getByRole('button', { name: 'Actions' }));
        const trigger = screen.getByRole('menuitem', { name: 'Mark Snoozed' });
        trigger.focus();
        await fireEvent.keyDown(trigger, { key: 'ArrowRight' });

        // Act
        await fireEvent.click(await screen.findByRole('menuitem', { name: '6 Hours' }));
        const dialogText = screen.getByRole('alertdialog').textContent?.replace(/\s+/g, ' ');
        await fireEvent.click(screen.getByRole('button', { name: 'Snooze Matching Stacks' }));

        // Assert
        expect(dialogText).toContain('Snooze for 6 hours, until ');
        await waitFor(() => expect(startBulk).toHaveBeenCalledOnce());
        const request = startBulk.mock.calls[0]![0];
        expect(request.type).toBe('stacks.snooze');
        const hours = (new Date(request.parameters.snooze_until_utc).getTime() - Date.now()) / 3600000;
        expect(hours).toBeGreaterThan(5.9);
        expect(hours).toBeLessThan(6.1);
    });

    it('starts mark fixed with the optional version and deletes with no parameters', async () => {
        // Arrange
        renderMatching();

        // Act
        await choose('Mark Fixed');
        await fireEvent.input(screen.getByLabelText('Version'), { target: { value: '1.2.3' } });
        await fireEvent.click(screen.getByRole('button', { name: 'Mark Matching Stacks Fixed' }));

        // Assert
        await waitFor(() =>
            expect(startBulk).toHaveBeenCalledWith({
                organization_id: 'organization-id',
                parameters: { version: '1.2.3' },
                selection,
                type: 'stacks.mark-fixed'
            })
        );
    });

    it('reports the problem and keeps the selection when the delete cannot start', async () => {
        // Arrange
        startBulk.mockRejectedValue({ status: 409, title: 'Another destructive operation is already running for this project.' });
        const { onAllMatchingStarted, resetRowSelection } = renderMatching();

        // Act
        await choose('Delete');
        await fireEvent.click(screen.getByRole('button', { name: 'Delete Matching Stacks' }));

        // Assert
        await waitFor(() =>
            expect(startBulk).toHaveBeenCalledWith({ organization_id: 'organization-id', parameters: undefined, selection, type: 'stacks.delete' })
        );
        await waitFor(() => expect(toast.error).toHaveBeenCalled());
        expect(onAllMatchingStarted).not.toHaveBeenCalled();
        expect(resetRowSelection).not.toHaveBeenCalled();
    });

    it('discards everything matching', async () => {
        // Arrange
        renderMatching();

        // Act
        await choose('Mark Discarded');
        await fireEvent.click(screen.getByRole('button', { name: 'Discard Matching Stacks' }));

        // Assert
        await waitFor(() =>
            expect(startBulk).toHaveBeenCalledWith({
                organization_id: 'organization-id',
                parameters: { status: 'discarded' },
                selection,
                type: 'stacks.change-status'
            })
        );
    });
});
