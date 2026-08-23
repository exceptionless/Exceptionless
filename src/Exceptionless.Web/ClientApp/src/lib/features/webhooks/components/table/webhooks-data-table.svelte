<script lang="ts">
    import type { Webhook } from '$features/webhooks/models';
    import type { Snippet } from 'svelte';

    import * as DataTable from '$comp/data-table';
    import DelayedRender from '$comp/delayed-render.svelte';
    import { type StockFeatures, type Table } from '@tanstack/svelte-table';

    interface Props {
        bodyChildren?: Snippet;
        footerChildren?: Snippet;
        isLoading: boolean;
        limit: number;
        rowClick?: (row: Webhook) => void;
        table: Table<StockFeatures, Webhook>;
        toolbarChildren?: Snippet;
    }

    let { bodyChildren, footerChildren, isLoading, limit = $bindable(), rowClick, table, toolbarChildren }: Props = $props();
</script>

<DataTable.Root>
    {#if toolbarChildren}
        <DataTable.Toolbar {table}>
            {@render toolbarChildren()}
        </DataTable.Toolbar>
    {:else}
        <DataTable.Toolbar {table} />
    {/if}
    <DataTable.Body {rowClick} {table}>
        {#if isLoading}
            <DelayedRender>
                <DataTable.Loading {table} />
            </DelayedRender>
        {:else}
            <DataTable.Empty {table} />
        {/if}
        {#if bodyChildren}
            {@render bodyChildren()}
        {/if}
    </DataTable.Body>
    <DataTable.Footer {table} class="w-full">
        {#if footerChildren}
            {@render footerChildren()}
        {:else}
            <DataTable.Selection {table} />
            <DataTable.Pager bind:value={limit} {table} />
        {/if}
    </DataTable.Footer>
</DataTable.Root>
