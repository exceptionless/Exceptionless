<script lang="ts">
    import { createEventDispatcher } from 'svelte';
    import { type Readable } from 'svelte/store';
    import { createSvelteTable } from '$comp/tanstack-table-svelte5';
    import * as DataTable from '$comp/data-table';
    import type { EventSummaryModel, SummaryTemplateKeys } from '$lib/models/api';
    import { type FetchClientResponse, FetchClient } from '$api/FetchClient';
    import WebSocketMessage from '$comp/messaging/WebSocketMessage.svelte';
    import ErrorMessage from '$comp/ErrorMessage.svelte';
    import { ChangeType, type WebSocketMessageValue } from '$lib/models/websocket';
    import CustomEventMessage from '$comp/messaging/CustomEventMessage.svelte';
    import Muted from '$comp/typography/Muted.svelte';
    import { getTableContext } from './options.svelte';
    import { DEFAULT_LIMIT } from '$lib/helpers/api';

    export let filter: Readable<string>;
    export let limit: Readable<number>;

    const context = getTableContext<EventSummaryModel<SummaryTemplateKeys>>({ mode: 'summary', limit: $limit }, (options) => ({
        ...options,
        columns: options.columns.filter((c) => c.id !== 'select').map((c) => ({ ...c, enableSorting: false })),
        enableRowSelection: false,
        enableMultiRowSelection: false,
        manualSorting: false
    }));
    const table = createSvelteTable(context.options);

    const { getJSON, loading } = new FetchClient();
    let response: FetchClientResponse<EventSummaryModel<SummaryTemplateKeys>[]>;
    let before: string | undefined;

    context.parameters.subscribe(async () => await loadData(true));
    filter.subscribe(async () => await loadData(true));

    async function loadData(filterChanged: boolean = false) {
        if ($loading && filterChanged && !before) {
            return;
        }

        if (filterChanged) {
            before = undefined;
        }

        response = await getJSON<EventSummaryModel<SummaryTemplateKeys>[]>('events', {
            params: {
                ...context.parameters,
                filter: $filter,
                before
            }
        });

        if (response.ok) {
            before = response.meta.links.previous?.before;

            const data = filterChanged ? [] : [...context.data];
            for (const summary of response.data?.reverse() || []) {
                data.push(summary);
            }

            const limit = context.parameters.limit ?? DEFAULT_LIMIT;
            const total = (response.meta?.total as number) ?? 0;
            context.data = data.slice(-limit);
            context.pageCount = Math.ceil(total / limit);
            context.meta = response.meta;
            context.loading = false;
        }
    }

    async function onPersistentEvent({ detail }: CustomEvent<WebSocketMessageValue<'PersistentEventChanged'>>) {
        switch (detail.change_type) {
            case ChangeType.Added:
            case ChangeType.Saved:
                return await loadData();
            case ChangeType.Removed:
                table.options.data = table.options.data.filter((doc) => doc.id !== detail.id);
                break;
        }
    }

    const dispatch = createEventDispatcher();
</script>

<CustomEventMessage type="refresh" on:message={() => loadData()}></CustomEventMessage>
<WebSocketMessage type="PersistentEventChanged" on:message={onPersistentEvent}></WebSocketMessage>

<DataTable.Root>
    <DataTable.Toolbar {table}>
        <slot name="toolbar" />
    </DataTable.Toolbar>
    <DataTable.Body {table} on:rowclick={(event) => dispatch('rowclick', event.detail)}></DataTable.Body>
    <Muted class="flex flex-1 items-center justify-between">
        <DataTable.PageSize {table} bind:value={$limit}></DataTable.PageSize>
        <Muted class="py-2 text-center">
            {#if response?.problem?.errors.general}
                <ErrorMessage message={response?.problem?.errors.general}></ErrorMessage>
            {/if}
        </Muted>
        <div></div>
    </Muted>
</DataTable.Root>
