<script module lang="ts">
    type TData = RowData;
</script>

<script generics="TData extends RowData" lang="ts">
    import type { Snippet } from 'svelte';

    import * as Table from '$comp/ui/table';
    import { type RowData, type StockFeatures, type Table as SvelteTable } from '@tanstack/svelte-table';

    interface Props {
        children?: Snippet;
        table: SvelteTable<StockFeatures, TData>;
    }

    let { children, table }: Props = $props();
</script>

<Table.Row class="hidden text-center only:table-row">
    <Table.Cell colspan={table.getVisibleLeafColumns().length}>
        {#if children}
            {@render children()}
        {:else}
            No data was found with the current filter.
        {/if}
    </Table.Cell>
</Table.Row>
