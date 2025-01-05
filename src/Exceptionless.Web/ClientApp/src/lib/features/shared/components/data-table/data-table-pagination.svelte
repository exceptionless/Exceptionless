<script module lang="ts">
    type TData = unknown;
</script>

<script generics="TData" lang="ts">
    import type { Table } from '@tanstack/svelte-table';

    import Number from '$comp/formatters/Number.svelte';
    import { Button } from '$comp/ui/button';
    import IconChevronDoubleLeft from '~icons/mdi/chevron-double-left';
    import IconChevronLeft from '~icons/mdi/chevron-left';
    import IconChevronRight from '~icons/mdi/chevron-right';

    interface Props {
        table: Table<TData>;
    }

    let { table }: Props = $props();
</script>

<div class="flex items-center space-x-6 lg:space-x-8">
    <div class="flex w-[100px] items-center justify-center text-sm font-medium">
        Page <Number value={table.getState().pagination.pageIndex + 1} /> of <Number value={table.getPageCount()} />
    </div>
    <div class="flex items-center space-x-2">
        {#if table.getState().pagination.pageIndex > 1}
            <Button class="hidden size-8 p-0 lg:flex" onclick={() => table.resetPageIndex(true)} variant="outline">
                <span class="sr-only">Go to first page</span>
                <IconChevronDoubleLeft class="mr-2 size-4" />
            </Button>
        {/if}
        <Button class="size-8 p-0" disabled={!table.getCanPreviousPage()} onclick={() => table.previousPage()} variant="outline">
            <span class="sr-only">Go to previous page</span>
            <IconChevronLeft class="size-4" />
        </Button>
        <Button class="size-8 p-0" disabled={!table.getCanNextPage()} onclick={() => table.nextPage()} variant="outline">
            <span class="sr-only">Go to next page</span>
            <IconChevronRight class="size-4" />
        </Button>
    </div>
</div>
