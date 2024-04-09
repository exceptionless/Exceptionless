<script lang="ts">
    import { derived, type Readable } from 'svelte/store';
    import type { Table } from '@tanstack/svelte-table';

    import { Button } from '$comp/ui/button';
    import IconChevronLeft from '~icons/mdi/chevron-left';
    import IconChevronRight from '~icons/mdi/chevron-right';
    import IconChevronDoubleLeft from '~icons/mdi/chevron-double-left';
    import Number from '$comp/formatters/Number.svelte';

    type TData = $$Generic;
    export let table: Readable<Table<TData>>;

    const pageIndex = derived(table, ($table) => $table.getState().pagination.pageIndex);
    const rows = derived(table, ($table) => $table.getRowModel().rows);
    const selectedRows = derived(table, ($table) => $table.getSelectedRowModel().rows);
</script>

<div class="flex items-center justify-between px-2">
    <div class="flex-1 text-sm text-muted-foreground">
        {#if $selectedRows.length > 0}
            {Object.keys($selectedRows).length} of{' '}
            {$rows.length} row(s) selected.
        {/if}
    </div>
    <div class="flex items-center space-x-6 lg:space-x-8">
        <slot />

        <div class="flex w-[100px] items-center justify-center text-sm font-medium">
            Page <Number value={$pageIndex + 1} /> of <Number value={$table.getPageCount()} />
        </div>
        <div class="flex items-center space-x-2">
            {#if $pageIndex > 1}
                <Button variant="outline" class="hidden h-8 w-8 p-0 lg:flex" on:click={() => $table.resetPageIndex(true)}>
                    <span class="sr-only">Go to first page</span>
                    <IconChevronDoubleLeft class="mr-2 h-4 w-4" />
                </Button>
            {/if}
            <Button variant="outline" class="h-8 w-8 p-0" disabled={!$table.getCanPreviousPage()} on:click={() => $table.previousPage()}>
                <span class="sr-only">Go to previous page</span>
                <IconChevronLeft class="h-4 w-4" />
            </Button>
            <Button variant="outline" class="h-8 w-8 p-0" disabled={!$table.getCanNextPage()} on:click={() => $table.nextPage()}>
                <span class="sr-only">Go to next page</span>
                <IconChevronRight class="h-4 w-4" />
            </Button>
        </div>
    </div>
</div>
