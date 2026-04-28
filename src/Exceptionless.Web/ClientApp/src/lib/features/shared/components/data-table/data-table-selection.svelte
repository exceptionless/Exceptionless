<script module lang="ts">
    type TData = RowData;
</script>

<script generics="TData extends RowData" lang="ts">
    import type { RowData, StockFeatures, Table } from '@tanstack/svelte-table';

    import Number from '$comp/formatters/number.svelte';

    interface Props {
        table: Table<StockFeatures, TData>;
    }

    let { table }: Props = $props();
</script>

<div class="text-muted-foreground flex min-w-0 text-sm text-pretty">
    {#if table.getSelectedRowModel().rows.length > 0}
        <span class="truncate"><Number value={Object.keys(table.getSelectedRowModel().rows).length} /> selected</span>
    {/if}
</div>
