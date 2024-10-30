<script lang="ts">
    import type { Snippet } from 'svelte';

    import * as DataTable from '$comp/data-table';
    import { DEFAULT_LIMIT } from '$shared/api';
    import { type FetchClientResponse, useFetchClient } from '@exceptionless/fetchclient';
    import { createTable } from '@tanstack/svelte-table';
    import { useEventListener } from 'runed';

    import type { GetEventsMode } from '../../api.svelte';
    import type { EventSummaryModel, SummaryTemplateKeys } from '../summary/index';

    import { getTableContext } from './options.svelte';

    interface Props {
        filter: string;
        limit: number;
        mode?: GetEventsMode;
        pageFilter?: string;
        rowclick?: (row: EventSummaryModel<SummaryTemplateKeys>) => void;
        time: string;
        toolbarChildren?: Snippet;
    }

    let { filter, limit = $bindable(DEFAULT_LIMIT), mode = 'summary', pageFilter = undefined, rowclick, time, toolbarChildren }: Props = $props();
    const context = getTableContext<EventSummaryModel<SummaryTemplateKeys>>({ limit, mode });
    const table = createTable(context.options);

    const client = useFetchClient();
    let response: FetchClientResponse<EventSummaryModel<SummaryTemplateKeys>[]>;

    $effect(() => {
        limit = Number(context.limit);
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
    <DataTable.Body {rowclick} {table}></DataTable.Body>
    <DataTable.Pagination {table}>
        <DataTable.PageSize bind:value={context.limit} {table}></DataTable.PageSize>
    </DataTable.Pagination>
</DataTable.Root>
