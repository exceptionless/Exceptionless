<script lang="ts">
    import type { OAuthApplication } from '$features/admin/models';
    import type { Snippet } from 'svelte';

    import * as DataTable from '$comp/data-table';
    import DelayedRender from '$comp/delayed-render.svelte';
    import { type StockFeatures, type Table } from '@tanstack/svelte-table';

    interface Props {
        isLoading: boolean;
        limit: number;
        rowClick?: (row: OAuthApplication, event?: MouseEvent) => void;
        rowHref?: (row: OAuthApplication) => string;
        table: Table<StockFeatures, OAuthApplication>;
        toolbarChildren?: Snippet;
    }

    let { isLoading, limit = $bindable(), rowClick, rowHref, table, toolbarChildren }: Props = $props();
</script>

<DataTable.Root>
    {#if toolbarChildren}
        <DataTable.Toolbar {table}>
            {@render toolbarChildren()}
        </DataTable.Toolbar>
    {/if}
    <DataTable.Footer {table} class="w-full">
        <DataTable.Pager bind:value={limit} {table} />
    </DataTable.Footer>
    <DataTable.Body {rowClick} {rowHref} {table}>
        {#if isLoading}
            <DelayedRender>
                <DataTable.Loading {table} />
            </DelayedRender>
        {:else}
            <DataTable.Empty {table}>No OAuth applications found.</DataTable.Empty>
        {/if}
    </DataTable.Body>
</DataTable.Root>
