<script lang="ts">
    import type { ElasticsearchSnapshot } from '$features/admin/models';
    import type { Snippet } from 'svelte';

    import * as DataTable from '$comp/data-table';
    import DelayedRender from '$comp/delayed-render.svelte';
    import { type Table } from '@tanstack/svelte-table';

    interface Props {
        isLoading: boolean;
        limit: number;
        table: Table<ElasticsearchSnapshot>;
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
    <DataTable.Body {table}>
        {#if isLoading}
            <DelayedRender>
                <DataTable.Loading {table} />
            </DelayedRender>
        {:else}
            <DataTable.Empty {table} />
        {/if}
    </DataTable.Body>
    <DataTable.Footer {table} class="space-x-6 lg:space-x-8">
        <DataTable.PageSize bind:value={limit} {table} />
        <div class="flex items-center space-x-6 lg:space-x-8">
            <DataTable.PageCount {table} />
            <DataTable.Pagination {table} />
        </div>
    </DataTable.Footer>
</DataTable.Root>
