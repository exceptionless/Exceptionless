<script lang="ts">
    import { type StockFeatures, type Table } from '@tanstack/svelte-table';

    import type { ShardMetric } from '#features/admin/models.ts';

    import * as DataTable from '#comp/data-table/index.ts';
    import DelayedRender from '#comp/delayed-render.svelte';

    interface Props {
        isLoading: boolean;
        rowClick?: (row: ShardMetric) => void;
        table: Table<StockFeatures, ShardMetric>;
    }

    let { isLoading, rowClick, table }: Props = $props();
</script>

<DataTable.Root>
    <DataTable.Body {rowClick} {table}>
        {#if isLoading}
            <DelayedRender>
                <DataTable.Loading {table} />
            </DelayedRender>
        {:else}
            <DataTable.Empty {table} />
        {/if}
    </DataTable.Body>
</DataTable.Root>
