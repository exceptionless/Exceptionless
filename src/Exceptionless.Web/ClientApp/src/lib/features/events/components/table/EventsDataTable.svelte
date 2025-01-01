<script lang="ts">
    import type { Snippet } from 'svelte';

    import * as DataTable from '$comp/data-table';
    import DelayedRender from '$comp/DelayedRender.svelte';
    import { type Table } from '@tanstack/svelte-table';

    import type { EventSummaryModel, SummaryTemplateKeys } from '../summary/index';

    interface Props {
        bodyChildren?: Snippet;
        isLoading: boolean;
        limit: number;
        rowClick?: (row: EventSummaryModel<SummaryTemplateKeys>) => void;
        table: Table<EventSummaryModel<SummaryTemplateKeys>>;
        toolbarChildren?: Snippet;
    }

    let { bodyChildren, isLoading, limit = $bindable(), rowClick, table, toolbarChildren }: Props = $props();
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
    <DataTable.Pagination {table}>
        <DataTable.PageSize bind:value={limit} {table}></DataTable.PageSize>
    </DataTable.Pagination>
</DataTable.Root>
