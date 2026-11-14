import { createTable, stockFeatures, type StockFeatures, type Table, tableFeatures } from '@tanstack/svelte-table';
import { describe, expect, it } from 'vitest';

import { createAllMatchingSelection, getEventsQueryIdentity } from './all-matching-selection.svelte';

describe('createAllMatchingSelection', () => {
    it('is active only for the query it was made for', () => {
        // Arrange
        const identity = getEventsQueryIdentity({ filter: 'type:error', organizationId: 'organization-id', time: '[now-7d TO now]' });
        const selection = createAllMatchingSelection(() => identity);

        // Act
        selection.select(1234);

        // Assert
        expect(selection.active).toEqual({ count: 1234, identity });
    });

    it.each([
        ['filter', { filter: 'type:log', organizationId: 'organization-id', time: '[now-7d TO now]' }],
        ['time range', { filter: 'type:error', organizationId: 'organization-id', time: '[now-30d TO now]' }],
        ['project filter', { filter: 'type:error project:project-b', organizationId: 'organization-id', time: '[now-7d TO now]' }],
        ['saved view', { filter: 'type:error', organizationId: 'organization-id', savedViewId: 'view-2', time: '[now-7d TO now]' }],
        ['organization', { filter: 'type:error', organizationId: 'another-organization', time: '[now-7d TO now]' }]
    ])('is dropped when the %s changes', (_, changed) => {
        // Arrange
        let identity = getEventsQueryIdentity({ filter: 'type:error', organizationId: 'organization-id', time: '[now-7d TO now]' });
        const selection = createAllMatchingSelection(() => identity);
        selection.select(1234);

        // Act
        identity = getEventsQueryIdentity(changed);

        // Assert
        expect(selection.active).toBeUndefined();
    });

    it('ignores a count that was fetched for a filter that is no longer showing', () => {
        // Arrange
        let identity = getEventsQueryIdentity({ filter: 'type:error', organizationId: 'organization-id' });
        const selection = createAllMatchingSelection(() => identity);
        const selectionWhenCountingStarted = selection.snapshot;

        // Act
        identity = getEventsQueryIdentity({ filter: 'type:log', organizationId: 'organization-id' });
        const selected = selection.select(1234, selectionWhenCountingStarted);

        // Assert
        expect(selected).toBe(false);
        expect(selection.active).toBeUndefined();
    });

    it('does not come back when the original query is restored after being cleared', () => {
        // Arrange
        const original = getEventsQueryIdentity({ filter: 'type:error', organizationId: 'organization-id' });
        let identity = original;
        const selection = createAllMatchingSelection(() => identity);
        selection.select(10);

        // Act
        identity = getEventsQueryIdentity({ filter: 'type:log', organizationId: 'organization-id' });
        selection.clear();
        identity = original;

        // Assert
        expect(selection.active).toBeUndefined();
    });

    it.each([
        ['unchanged', false],
        ['cleared and selected again', true]
    ])('handles a deferred count when the page selection is %s', async (_, changeSelection) => {
        // Arrange
        const identity = getEventsQueryIdentity({ filter: 'type:error', organizationId: 'organization-id' });
        let table: Table<StockFeatures, { id: string }>;
        const dispose = $effect.root(() => {
            table = createTable({
                columns: [{ accessorKey: 'id' }],
                data: [{ id: 'event-1' }, { id: 'event-2' }],
                features: tableFeatures(stockFeatures),
                getRowId: (row) => row.id
            });
        });
        table!.toggleAllRowsSelected(true);
        const selection = createAllMatchingSelection(
            () => identity,
            () => table!.store.state.rowSelection
        );
        const counting = Promise.withResolvers<number>();
        const selectionWhenCountingStarted = selection.snapshot;
        const selecting = counting.promise.then((count) => selection.select(count, selectionWhenCountingStarted));

        // Act
        if (changeSelection) {
            table!.resetRowSelection();
            table!.toggleAllRowsSelected(true);
        }
        counting.resolve(1234);
        const selected = await selecting;
        dispose();

        // Assert
        expect(table!.getIsAllRowsSelected()).toBe(true);
        expect(selected).toBe(!changeSelection);
        expect(selection.active).toEqual(changeSelection ? undefined : { count: 1234, identity });
    });
});
