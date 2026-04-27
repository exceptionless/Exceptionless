<script lang="ts">
    import type { ShardMetric } from '$features/admin/models';

    import * as DataTable from '$comp/data-table';
    import DelayedRender from '$comp/delayed-render.svelte';
    import { type StockFeatures, type Table } from '@tanstack/svelte-table';

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
