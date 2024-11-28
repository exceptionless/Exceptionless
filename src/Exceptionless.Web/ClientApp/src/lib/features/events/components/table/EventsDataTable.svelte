<script lang="ts">
    import type { Snippet } from 'svelte';

    import * as DataTable from '$comp/data-table';
    import { type Table } from '@tanstack/svelte-table';

    import type { EventSummaryModel, SummaryTemplateKeys } from '../summary/index';

    interface Props {
        limit: number;
        rowclick?: (row: EventSummaryModel<SummaryTemplateKeys>) => void;
        table: Table<EventSummaryModel<SummaryTemplateKeys>>;
        toolbarChildren?: Snippet;
    }

    let { limit = $bindable(), rowclick, table, toolbarChildren }: Props = $props();
</script>

<DataTable.Root>
    <DataTable.Toolbar {table}>
        {#if toolbarChildren}
            {@render toolbarChildren()}
        {/if}
    </DataTable.Toolbar>
    <DataTable.Body {rowclick} {table}></DataTable.Body>
    <DataTable.Pagination {table}>
        <DataTable.PageSize bind:value={limit} {table}></DataTable.PageSize>
    </DataTable.Pagination>
</DataTable.Root>
