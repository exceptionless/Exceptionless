<script lang="ts">
    import { createSvelteTable } from '$comp/tanstack-table-svelte5';
    import { createEventDispatcher } from 'svelte';
    import { FetchClient, type FetchClientResponse } from '$api/FetchClient.svelte';
    import CustomEventMessage from '$comp/messaging/CustomEventMessage.svelte';

    import * as DataTable from '$comp/data-table';
    import { getTableContext } from './options.svelte';
    import { DEFAULT_LIMIT } from '$lib/helpers/api';
    import type { EventSummaryModel, GetEventsMode, IGetEventsParams, SummaryTemplateKeys } from '$lib/models/api';

    let {
        filter,
        pageFilter = undefined,
        limit = DEFAULT_LIMIT,
        time,
        mode = 'summary'
    }: { filter: string; pageFilter: string | undefined; limit: number; time: string; mode: GetEventsMode } = $props();

    const parameters = $state.frozen({ mode, limit } as IGetEventsParams);
    const context = getTableContext<EventSummaryModel<SummaryTemplateKeys>>(parameters);
    const table = createSvelteTable(context.options);

    const { getJSON, loading } = new FetchClient();
    let response: FetchClientResponse<EventSummaryModel<SummaryTemplateKeys>[]>;

    $effect(() => {
        loadData();
    });

    async function loadData() {
        if ($loading) {
            return;
        }

        console.log({
            ...parameters,
            filter: [pageFilter, filter].filter(Boolean).join(' '),
            time
        });
        response = await getJSON<EventSummaryModel<SummaryTemplateKeys>[]>('events', {
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

    const dispatch = createEventDispatcher();
</script>

<CustomEventMessage type="refresh" on:message={loadData}></CustomEventMessage>

<DataTable.Root>
    <DataTable.Toolbar {table}>
        <slot name="toolbar" />
    </DataTable.Toolbar>
    <DataTable.Body {table} on:rowclick={(event) => dispatch('rowclick', event.detail)}></DataTable.Body>
    <DataTable.Pagination {table}>
        <DataTable.PageSize {table} bind:value={parameters.limit}></DataTable.PageSize>
    </DataTable.Pagination>
</DataTable.Root>
