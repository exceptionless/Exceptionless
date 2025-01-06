<script module lang="ts">
    type TData = unknown;
</script>

<script generics="TData" lang="ts">
    import type { Table } from '@tanstack/svelte-table';

    interface Props {
        table: Table<TData>;
    }

    let { table }: Props = $props();
</script>

<div class="flex text-pretty text-sm text-muted-foreground">
    {#if table.getSelectedRowModel().rows.length > 0}
        {Object.keys(table.getSelectedRowModel().rows).length} of{' '}
        {table.getRowModel().rows.length} row(s) selected.
    {/if}
</div>
