<script lang="ts">
    import { useFetchClient, type FetchClientResponse } from '@exceptionless/fetchclient';
    import { createSvelteTable } from '@tanstack/svelte-table';
    import type { Snippet } from 'svelte';
    import { useEventListener } from 'runed';

    import * as DataTable from '$comp/data-table';
    import { getTableContext } from './options.svelte';
    import { DEFAULT_LIMIT } from '$lib/helpers/api';
    import type { EventSummaryModel, GetEventsMode, IGetEventsParams, SummaryTemplateKeys } from '$lib/models/api';

    interface Props {
        filter: string;
        pageFilter?: string;
        limit: number;
        time: string;
        mode?: GetEventsMode;
        toolbarChildren?: Snippet;
        rowclick?: (row: EventSummaryModel<SummaryTemplateKeys>) => void;
    }

    let { filter, pageFilter = undefined, limit = DEFAULT_LIMIT, time, mode = 'summary', rowclick, toolbarChildren }: Props = $props();

    const parameters = $state({ mode, limit } as IGetEventsParams);
    const context = getTableContext<EventSummaryModel<SummaryTemplateKeys>>(parameters);
    const table = createSvelteTable(context.options);

    const client = useFetchClient();
    let response: FetchClientResponse<EventSummaryModel<SummaryTemplateKeys>[]>;

    $effect(() => {
        loadData();
    });

    async function loadData() {
        if (client.loading) {
            return;
        }

        response = await client.getJSON<EventSummaryModel<SummaryTemplateKeys>[]>('events', {
            params: {
                ...parameters,
                filter: [pageFilter, filter].filter(Boolean).join(' '),
                time
            }
        });

        if (response.ok) {
            const limit = parameters.limit ?? DEFAULT_LIMIT;
            const total = (response.meta?.total as number) ?? 0;
            context.data = response.data || [];
            context.pageCount = Math.ceil(total / limit);
            context.meta = response.meta;
            context.loading = false;

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
        <DataTable.PageSize {table} bind:value={parameters.limit}></DataTable.PageSize>
    </DataTable.Pagination>
</DataTable.Root>
