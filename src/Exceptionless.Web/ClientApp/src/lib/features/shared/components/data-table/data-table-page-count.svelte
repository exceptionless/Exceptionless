<script module lang="ts">
    type TData = unknown;
</script>

<script generics="TData" lang="ts">
    import type { Table } from '@tanstack/svelte-table';

    import Number from '$comp/formatters/Number.svelte';

    interface Props {
        table: Table<TData>;
    }

    let { table }: Props = $props();
</script>

<div class="flex w-[100px] items-center justify-center text-sm font-medium">
    Page <Number value={table.getState().pagination.pageIndex + 1} /> of <Number value={table.getPageCount()} />
</div>
