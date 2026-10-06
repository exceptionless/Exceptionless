<script module lang="ts">
    type TData = RowData;
</script>

<script generics="TData extends RowData" lang="ts">
    import type { Snippet } from 'svelte';

    import * as Table from '$comp/ui/table';
    import { type RowData, type StockFeatures, type Table as SvelteTable } from '@tanstack/svelte-table';

    import { getDataTableLayoutContext } from './data-table-layout-context.svelte';

    interface Props {
        children?: Snippet;
        table: SvelteTable<StockFeatures, TData>;
    }

    let { children, table }: Props = $props();

    const dataTableLayout = getDataTableLayoutContext();
    const columnCount = $derived(table.getVisibleLeafColumns().length + (dataTableLayout?.getFillerColumnCount() ?? 0));
</script>

<Table.Row class="hidden text-center only:table-row">
    <Table.Cell colspan={columnCount}>
        {#if children}
            {@render children()}
        {:else}
            No data was found with the current filter.
        {/if}
    </Table.Cell>
</Table.Row>
