<script module lang="ts">
    type TData = unknown;
</script>

<script generics="TData" lang="ts">
    import type { Table } from '@tanstack/svelte-table';
    import type { Snippet } from 'svelte';

    import Number from '$comp/formatters/Number.svelte';
    import { Button } from '$comp/ui/button';
    import IconChevronDoubleLeft from '~icons/mdi/chevron-double-left';
    import IconChevronLeft from '~icons/mdi/chevron-left';
    import IconChevronRight from '~icons/mdi/chevron-right';

    interface Props {
        children: Snippet;
        table: Table<TData>;
    }

    let { children, table }: Props = $props();
</script>

<div class="flex items-center justify-between px-2">
    <div class="flex-1 text-sm text-muted-foreground">
        {#if table.getSelectedRowModel().rows.length > 0}
            {Object.keys(table.getSelectedRowModel().rows).length} of{' '}
            {table.getRowModel().rows.length} row(s) selected.
        {/if}
    </div>
    <div class="flex items-center space-x-6 lg:space-x-8">
        {@render children()}

        <div class="flex w-[100px] items-center justify-center text-sm font-medium">
            Page <Number value={table.getState().pagination.pageIndex + 1} /> of <Number value={table.getPageCount()} />
        </div>
        <div class="flex items-center space-x-2">
            {#if table.getState().pagination.pageIndex > 1}
                <Button class="hidden h-8 w-8 p-0 lg:flex" on:click={() => table.resetPageIndex(true)} variant="outline">
                    <span class="sr-only">Go to first page</span>
                    <IconChevronDoubleLeft class="mr-2 h-4 w-4" />
                </Button>
            {/if}
            <Button class="h-8 w-8 p-0" disabled={!table.getCanPreviousPage()} on:click={() => table.previousPage()} variant="outline">
                <span class="sr-only">Go to previous page</span>
                <IconChevronLeft class="h-4 w-4" />
            </Button>
            <Button class="h-8 w-8 p-0" disabled={!table.getCanNextPage()} on:click={() => table.nextPage()} variant="outline">
                <span class="sr-only">Go to next page</span>
                <IconChevronRight class="h-4 w-4" />
            </Button>
        </div>
    </div>
</div>
