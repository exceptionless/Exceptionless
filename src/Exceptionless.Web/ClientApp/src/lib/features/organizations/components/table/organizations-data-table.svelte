<script lang="ts">
    import type { ViewOrganization } from '$features/organizations/models';
    import type { Snippet } from 'svelte';

    import * as DataTable from '$comp/data-table';
    import DelayedRender from '$comp/delayed-render.svelte';
    import { type StockFeatures, type Table } from '@tanstack/svelte-table';

    interface Props {
        bodyChildren?: Snippet;
        footerChildren?: Snippet;
        isLoading: boolean;
        rowClick?: (row: ViewOrganization, event?: MouseEvent) => void;
        rowHref?: (row: ViewOrganization) => string;
        table: Table<StockFeatures, ViewOrganization>;
        toolbarChildren?: Snippet;
    }

    let { bodyChildren, footerChildren, isLoading, rowClick, rowHref, table, toolbarChildren }: Props = $props();
</script>

<DataTable.Root>
    {#if toolbarChildren}
        <DataTable.Toolbar {table}>
            {@render toolbarChildren()}
        </DataTable.Toolbar>
    {:else}
        <DataTable.Toolbar {table} />
    {/if}
    {#if footerChildren}
        <DataTable.Footer {table} class="gap-6 lg:gap-8">
            {@render footerChildren()}
        </DataTable.Footer>
    {/if}
    <DataTable.Body {rowClick} {rowHref} {table}>
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
</DataTable.Root>
