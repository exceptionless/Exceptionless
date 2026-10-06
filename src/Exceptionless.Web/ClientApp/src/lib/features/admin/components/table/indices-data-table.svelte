<script lang="ts">
    import type { ElasticsearchIndexDetail } from '$features/admin/models';
    import type { Snippet } from 'svelte';

    import * as DataTable from '$comp/data-table';
    import DelayedRender from '$comp/delayed-render.svelte';
    import { type StockFeatures, type Table } from '@tanstack/svelte-table';

    interface Props {
        isLoading: boolean;
        limit: number;
        table: Table<StockFeatures, ElasticsearchIndexDetail>;
        toolbarChildren?: Snippet;
    }

    let { isLoading, limit = $bindable(), table, toolbarChildren }: Props = $props();
</script>

<DataTable.Root>
    {#if toolbarChildren}
        <DataTable.Toolbar {table}>
            {@render toolbarChildren()}
        </DataTable.Toolbar>
    {:else}
        <DataTable.Toolbar {table} />
    {/if}
    <DataTable.Footer {table} class="w-full">
        <DataTable.Pager bind:value={limit} {table} />
    </DataTable.Footer>
    <DataTable.Body {table}>
        {#if isLoading}
            <DelayedRender>
                <DataTable.Loading {table} />
            </DelayedRender>
        {:else}
            <DataTable.Empty {table} />
        {/if}
    </DataTable.Body>
</DataTable.Root>
