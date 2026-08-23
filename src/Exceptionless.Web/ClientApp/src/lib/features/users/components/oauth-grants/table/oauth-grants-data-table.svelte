<script lang="ts">
    import type { OAuthGrant } from '$features/users/models';
    import type { Snippet } from 'svelte';

    import * as DataTable from '$comp/data-table';
    import DelayedRender from '$comp/delayed-render.svelte';
    import { type StockFeatures, type Table } from '@tanstack/svelte-table';

    interface Props {
        isLoading: boolean;
        limit: number;
        table: Table<StockFeatures, OAuthGrant>;
        toolbarChildren?: Snippet;
    }

    let { isLoading, limit = $bindable(), table, toolbarChildren }: Props = $props();
</script>

<DataTable.Root>
    {#if toolbarChildren}
        <DataTable.Toolbar {table}>
            {@render toolbarChildren()}
        </DataTable.Toolbar>
    {/if}
    <DataTable.Body {table}>
        {#if isLoading}
            <DelayedRender>
                <DataTable.Loading {table} />
            </DelayedRender>
        {:else}
            <DataTable.Empty {table}>No applications have access to your account.</DataTable.Empty>
        {/if}
    </DataTable.Body>
    <DataTable.Footer {table} class="w-full">
        <DataTable.Selection {table} />
        <DataTable.Pager bind:value={limit} {table} />
    </DataTable.Footer>
</DataTable.Root>
