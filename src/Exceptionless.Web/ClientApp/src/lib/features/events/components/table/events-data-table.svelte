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
        toolbarActions?: Snippet;
        toolbarChildren?: Snippet;
    }

    let { bodyChildren, footerChildren, isLoading, limit = $bindable(), rowClick, table, toolbarActions, toolbarChildren }: Props = $props();
</script>

<DataTable.Root>
    <DataTable.Toolbar {table}>
        {#if toolbarChildren}
            {@render toolbarChildren()}
        {/if}
        {#snippet actions()}
            {#if toolbarActions}
                {@render toolbarActions()}
            {/if}
        {/snippet}
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
    <DataTable.Footer {table} class="w-full">
        {#if footerChildren}
            {@render footerChildren()}
        {:else}
            <div class="w-full grid grid-cols-1 sm:grid-cols-3 gap-2 items-center">
                <div class="flex items-center gap-2 min-w-0">
                    <DataTable.Selection {table} />
                </div>

                <div class="flex items-center justify-center min-w-0">
                    <DataTable.PageCount {table} />
                </div>

                <div class="flex items-center justify-end gap-4 min-w-0">
                    <DataTable.PageSize bind:value={limit} {table} />
                    <DataTable.Pagination {table} />
                </div>
            </div>
        {/if}
    </DataTable.Footer>
</DataTable.Root>
