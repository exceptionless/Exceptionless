<script lang="ts">
    import type { Snippet } from 'svelte';

    import * as DataTable from '$comp/data-table';
    import ErrorMessage from '$comp/ErrorMessage.svelte';
    import { getKeywordFilter, getOrganizationFilter, getProjectFilter, getStackFilter, type IFilter } from "$comp/filters/filters.svelte";
    import { Muted } from '$comp/typography';
    import { ChangeType, type WebSocketMessageValue } from '$features/websockets/models';
    import { DEFAULT_LIMIT } from '$shared/api';
    import { type FetchClientResponse, useFetchClient } from '@exceptionless/fetchclient';
    import { createTable } from '@tanstack/svelte-table';
    import { useEventListener } from 'runed';
    import { debounce } from "throttle-debounce";

    import type { EventSummaryModel, SummaryTemplateKeys } from '../summary/index';

    import { getTableContext } from './options.svelte';

    interface Props {
        filter: string;
        filters: IFilter[];
        limit: number;
        rowclick?: (row: EventSummaryModel<SummaryTemplateKeys>) => void;
        toolbarChildren?: Snippet;
    }

    let { filter, filters, limit = $bindable(DEFAULT_LIMIT), rowclick, toolbarChildren }: Props = $props();
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
        limit = Number(context.limit);
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
            if (response.meta.links.previous?.before) {
                before = response.meta.links.previous?.before;
            }

            const data = filterChanged ? [] : [...context.data];
            for (const summary of response.data?.reverse() || []) {
                data.push(summary);
            }

            context.data = data.slice(-context.limit);
            context.meta = response.meta;
        }
    }

    const debouncedLoadData = debounce(5000, loadData);

    async function onPersistentEvent(message: WebSocketMessageValue<'PersistentEventChanged'>) {
        const shouldRefresh = () => {
            if (!filter) {
                return true;
            }

            const { id, organization_id, project_id, stack_id } = message;
            if (id) {
                // Check to see if any records on the page match
                if (table.options.data.some((doc) => doc.id === id)) {
                    return true;
                }

                // This could match any kind of lucene query (even must not filtering)
                const keywordFilter = getKeywordFilter(filters);
                if (keywordFilter && !keywordFilter.isEmpty()) {
                    if (keywordFilter.value!.includes(id)) {
                        return true;
                    }
                }
            }

            if (stack_id) {
                const stackFilter = getStackFilter(filters);
                if (stackFilter && !stackFilter.isEmpty()) {
                    return stackFilter.value === stack_id;
                }
            }

            if (project_id) {
                const projectFilter = getProjectFilter(filters);
                if (projectFilter && !projectFilter.isEmpty()) {
                    return projectFilter.value.includes(project_id);
                }
            }

            if (organization_id) {
                const organizationFilter = getOrganizationFilter(filters);
                if (organizationFilter && !organizationFilter.isEmpty()) {
                    return organizationFilter.value === organization_id;
                }
            }

            return true;
        };

        switch (message.change_type) {
            case ChangeType.Added:
            case ChangeType.Saved:
                if (shouldRefresh()) {
                    await debouncedLoadData();
                }

                break;
            case ChangeType.Removed:
                if (shouldRefresh()) {
                    if (message.id) {
                        table.options.data = table.options.data.filter((doc) => doc.id !== message.id);
                    }

                    await debouncedLoadData();
                }

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
