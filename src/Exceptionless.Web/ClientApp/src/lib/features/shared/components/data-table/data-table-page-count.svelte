<script module lang="ts">
    type TData = unknown;
</script>

<script generics="TData" lang="ts">
    import type { Table } from '@tanstack/svelte-table';

    import Number from '$comp/formatters/number.svelte';

    interface Props {
        table: Table<TData>;
    }

    let { table }: Props = $props();
</script>

<div class="min-w-0 text-sm font-medium">
    <div class="hidden sm:flex items-center justify-center">
        <span class="truncate">Page <Number value={table.getState().pagination.pageIndex + 1} /> of <Number value={table.getPageCount()} /></span>
    </div>

    <div class="flex sm:hidden items-center justify-center">
        <span aria-hidden="true" class="truncate"><Number value={table.getState().pagination.pageIndex + 1} /> / <Number value={table.getPageCount()} /></span>
    </div>

    <span class="sr-only">Page <Number value={table.getState().pagination.pageIndex + 1} /> of <Number value={table.getPageCount()} /></span>
</div>
