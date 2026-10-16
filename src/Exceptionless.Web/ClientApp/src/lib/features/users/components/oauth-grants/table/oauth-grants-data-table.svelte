<script lang="ts">
    import type { Snippet } from 'svelte';

    import { type StockFeatures, type Table } from '@tanstack/svelte-table';

    import type { OAuthGrant } from '#features/users/models.ts';

    import * as DataTable from '#comp/data-table/index.ts';
    import DelayedRender from '#comp/delayed-render.svelte';

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
    <DataTable.Footer {table} class="w-full">
        <DataTable.Selection {table} />
        <DataTable.Pager bind:value={limit} {table} />
    </DataTable.Footer>
    <DataTable.Body {table}>
        {#if isLoading}
            <DelayedRender>
                <DataTable.Loading {table} />
            </DelayedRender>
        {:else}
            <DataTable.Empty {table}>No applications have access to your account.</DataTable.Empty>
        {/if}
    </DataTable.Body>
</DataTable.Root>
