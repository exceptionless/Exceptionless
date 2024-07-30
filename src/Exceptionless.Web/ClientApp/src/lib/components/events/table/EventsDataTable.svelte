<script lang="ts">
    import { useFetchClient, type FetchClientResponse } from '@exceptionless/fetchclient';
    import { createTable } from '@tanstack/svelte-table';
    import type { Snippet } from 'svelte';
    import { useEventListener } from 'runed';

    import * as DataTable from '$comp/data-table';
    import { getTableContext } from './options.svelte';
    import { DEFAULT_LIMIT } from '$lib/helpers/api';
    import type { EventSummaryModel, GetEventsMode, SummaryTemplateKeys } from '$lib/models/api';

    interface Props {
        filter: string;
        pageFilter?: string;
        limit: number;
        time: string;
        mode?: GetEventsMode;
        toolbarChildren?: Snippet;
        rowclick?: (row: EventSummaryModel<SummaryTemplateKeys>) => void;
    }

    let { filter, pageFilter = undefined, limit = $bindable(DEFAULT_LIMIT), time, mode = 'summary', rowclick, toolbarChildren }: Props = $props();
    const context = getTableContext<EventSummaryModel<SummaryTemplateKeys>>({ mode, limit });
    const table = createTable(context.options);

    const client = useFetchClient();
    let response: FetchClientResponse<EventSummaryModel<SummaryTemplateKeys>[]>;

    $effect(() => {
        limit = context.limit;
        loadData();
    });

    async function loadData() {
        if (client.loading) {
            return;
        }

        response = await client.getJSON<EventSummaryModel<SummaryTemplateKeys>[]>('events', {
            params: {
                ...context.parameters,
                filter: [pageFilter, filter].filter(Boolean).join(' '),
                time
            }
        });

        if (response.ok) {
            context.data = response.data || [];
            context.meta = response.meta;
            table.resetRowSelection();
        }
    }

    useEventListener(document, 'refresh', async () => await loadData());
</script>

<DataTable.Root>
    <DataTable.Toolbar {table}>
        {#if toolbarChildren}
            {@render toolbarChildren()}
        {/if}
    </DataTable.Toolbar>
    <DataTable.Body {table} {rowclick}></DataTable.Body>
    <DataTable.Pagination {table}>
        <DataTable.PageSize {table} bind:value={context.limit}></DataTable.PageSize>
    </DataTable.Pagination>
</DataTable.Root>
