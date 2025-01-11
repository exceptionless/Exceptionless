<script lang="ts">
    import type { Snippet } from 'svelte';

    import * as DataTable from '$comp/data-table';
    import DelayedRender from '$comp/delayed-render.svelte';
    import { type Table } from '@tanstack/svelte-table';

    import type { EventSummaryModel, SummaryTemplateKeys } from '../summary/index';

    interface Props {
        bodyChildren?: Snippet;
        footerChildren?: Snippet;
        isLoading: boolean;
        limit: number;
        rowClick?: (row: EventSummaryModel<SummaryTemplateKeys>) => void;
        table: Table<EventSummaryModel<SummaryTemplateKeys>>;
        toolbarChildren?: Snippet;
    }

    let { bodyChildren, footerChildren, isLoading, limit = $bindable(), rowClick, table, toolbarChildren }: Props = $props();
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
            <DataTable.PageSize bind:value={limit} {table}></DataTable.PageSize>
            <div class="flex items-center space-x-6 lg:space-x-8">
                <DataTable.PageCount {table} />
                <DataTable.Pagination {table} />
            </div>
        {/if}
    </DataTable.Footer>
</DataTable.Root>
