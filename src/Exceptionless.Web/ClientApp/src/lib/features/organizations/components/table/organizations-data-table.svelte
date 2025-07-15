<script lang="ts">
    import type { ViewOrganization } from '$features/organizations/models';
    import type { Snippet } from 'svelte';

    import * as DataTable from '$comp/data-table';
    import DelayedRender from '$comp/delayed-render.svelte';
    import { type Table } from '@tanstack/svelte-table';

    interface Props {
        bodyChildren?: Snippet;
        footerChildren?: Snippet;
        isLoading: boolean;
        rowClick?: (row: ViewOrganization) => void;
        table: Table<ViewOrganization>;
        toolbarChildren?: Snippet;
    }

    let { bodyChildren, footerChildren, isLoading, rowClick, table, toolbarChildren }: Props = $props();
</script>

<DataTable.Root>
    <DataTable.Toolbar {table}>
        {#if toolbarChildren}
            {@render toolbarChildren()}
        {/if}
    </DataTable.Toolbar>
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
    <DataTable.Footer {table} class="space-x-6 lg:space-x-8">
        {#if footerChildren}
            {@render footerChildren()}
        {:else}
            <DataTable.Selection {table} />
        {/if}
    </DataTable.Footer>
</DataTable.Root>
