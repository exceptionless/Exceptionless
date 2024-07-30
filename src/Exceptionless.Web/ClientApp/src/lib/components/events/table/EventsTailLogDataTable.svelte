<script lang="ts">
    import type { EventSummaryModel, SummaryTemplateKeys } from '$lib/models/api';
    import type { Snippet } from 'svelte';

    import * as DataTable from '$comp/data-table';
    import ErrorMessage from '$comp/ErrorMessage.svelte';
    import { Muted } from '$comp/typography';
    import { DEFAULT_LIMIT } from '$lib/helpers/api';
    import { ChangeType, type WebSocketMessageValue } from '$lib/models/websocket';
    import { type FetchClientResponse, useFetchClient } from '@exceptionless/fetchclient';
    import { createTable } from '@tanstack/svelte-table';
    import { useEventListener } from 'runed';

    import { getTableContext } from './options.svelte';

    interface Props {
        filter: string;
        limit: number;
        rowclick?: (row: EventSummaryModel<SummaryTemplateKeys>) => void;
        toolbarChildren?: Snippet;
    }

    let { filter, limit = $bindable(DEFAULT_LIMIT), rowclick, toolbarChildren }: Props = $props();
    const context = getTableContext<EventSummaryModel<SummaryTemplateKeys>>({ limit, mode: 'summary' }, (options) => ({
        ...options,
        columns: options.columns.filter((c) => c.id !== 'select').map((c) => ({ ...c, enableSorting: false })),
        enableMultiRowSelection: false,
        enableRowSelection: false,
        manualSorting: false
    }));
    const table = createTable(context.options);

    const client = useFetchClient();
    let response = $state<FetchClientResponse<EventSummaryModel<SummaryTemplateKeys>[]>>();
    let before: string | undefined;

    $effect(() => {
        limit = context.limit;
        loadData(true);
    });

    async function loadData(filterChanged: boolean = false) {
        if (client.loading && filterChanged && !before) {
            return;
        }

        if (filterChanged) {
            before = undefined;
        }

        response = await client.getJSON<EventSummaryModel<SummaryTemplateKeys>[]>('events', {
            params: {
                ...context.parameters,
                before,
                filter
            }
        });

        if (response.ok) {
            before = response.meta.links.previous?.before;

            const data = filterChanged ? [] : [...context.data];
            for (const summary of response.data?.reverse() || []) {
                data.push(summary);
            }

            context.data = data.slice(-context.limit);
            context.meta = response.meta;
        }
    }

    async function onPersistentEvent(message: WebSocketMessageValue<'PersistentEventChanged'>) {
        switch (message.change_type) {
            case ChangeType.Added:
            case ChangeType.Saved:
                return await loadData();
            case ChangeType.Removed:
                table.options.data = table.options.data.filter((doc) => doc.id !== message.id);
                break;
        }
    }

    useEventListener(document, 'refresh', async () => await loadData());
    useEventListener(document, 'PersistentEventChanged', (event) => onPersistentEvent((event as CustomEvent).detail));
</script>

<DataTable.Root>
    <DataTable.Toolbar {table}>
        {#if toolbarChildren}
            {@render toolbarChildren()}
        {/if}
    </DataTable.Toolbar>
    <DataTable.Body {rowclick} {table}></DataTable.Body>
    <Muted class="flex flex-1 items-center justify-between">
        <DataTable.PageSize bind:value={context.limit} {table}></DataTable.PageSize>
        <Muted class="py-2 text-center">
            {#if response?.problem?.errors.general}
                <ErrorMessage message={response?.problem?.errors.general}></ErrorMessage>
            {/if}
        </Muted>
        <div></div>
    </Muted>
</DataTable.Root>
