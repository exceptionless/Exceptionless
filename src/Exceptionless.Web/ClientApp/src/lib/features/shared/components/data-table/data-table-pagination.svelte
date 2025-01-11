<script module lang="ts">
    type TData = unknown;
</script>

<script generics="TData" lang="ts">
    import type { Table } from '@tanstack/svelte-table';

    import { Button } from '$comp/ui/button';
    import ChevronLeft from 'lucide-svelte/icons/chevron-left';
    import ChevronRight from 'lucide-svelte/icons/chevron-right';
    import ChevronDoubleLeft from 'lucide-svelte/icons/chevrons-left';

    interface Props {
        table: Table<TData>;
    }

    let { table }: Props = $props();
</script>

<div class="flex items-center space-x-2">
    {#if table.getState().pagination.pageIndex > 1}
        <Button class="hidden size-8 p-0 lg:flex" onclick={() => table.resetPageIndex(true)} variant="outline">
            <span class="sr-only">Go to first page</span>
            <ChevronDoubleLeft class="size-4" />
        </Button>
    {/if}
    <Button class="size-8 p-0" disabled={!table.getCanPreviousPage()} onclick={() => table.previousPage()} variant="outline">
        <span class="sr-only">Go to previous page</span>
        <ChevronLeft class="size-4" />
    </Button>
    <Button class="size-8 p-0" disabled={!table.getCanNextPage()} onclick={() => table.nextPage()} variant="outline">
        <span class="sr-only">Go to next page</span>
        <ChevronRight class="size-4" />
    </Button>
</div>
