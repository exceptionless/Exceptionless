<script module lang="ts">
    type TData = RowData;
</script>

<script generics="TData extends RowData" lang="ts">
    import type { Snippet } from 'svelte';

    import { Button } from '$comp/ui/button';
    import * as Table from '$comp/ui/table';
    import { type RowData, type StockFeatures, type Table as SvelteTable } from '@tanstack/svelte-table';

    import { getDataTableLayoutContext } from './data-table-layout-context.svelte';

    interface Props {
        children?: Snippet;
        refresh: () => Promise<void>;
        table: SvelteTable<StockFeatures, TData>;
    }

    let { children, refresh, table }: Props = $props();

    const dataTableLayout = getDataTableLayoutContext();
    const columnCount = $derived(table.getVisibleLeafColumns().length + (dataTableLayout?.getFillerColumnCount() ?? 0));
</script>

<Table.Row class="text-center">
    <Table.Cell colspan={columnCount}>
        {#if children}
            {@render children()}
        {:else}
            New data is available!
            <Button variant="link" onclick={refresh} class="px-0">Click here to see the latest changes and reset any selections.</Button>
        {/if}
    </Table.Cell>
</Table.Row>
