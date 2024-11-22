<script lang="ts">
    import type { Snippet } from 'svelte';

    import * as DataTable from '$comp/data-table';
    import { getKeywordFilter, getOrganizationFilter, getProjectFilter, getStackFilter, type IFilter } from "$comp/filters/filters.svelte";
    import { ChangeType, type WebSocketMessageValue } from '$features/websockets/models';
    import { DEFAULT_LIMIT } from '$shared/api';
    import { type FetchClientResponse, useFetchClient } from '@exceptionless/fetchclient';
    import { createTable } from '@tanstack/svelte-table';
    import { useEventListener } from 'runed';
    import { debounce } from 'throttle-debounce';

    import type { GetEventsMode } from '../../api.svelte';
    import type { EventSummaryModel, SummaryTemplateKeys } from '../summary/index';

    import { getTableContext } from './options.svelte';

    interface Props {
        filter: string;
        filters: IFilter[];
        limit: number;
        mode?: GetEventsMode;
        pageFilter?: string;
        rowclick?: (row: EventSummaryModel<SummaryTemplateKeys>) => void;
        time: string;
        toolbarChildren?: Snippet;
    }

    let { filter, filters, limit = $bindable(DEFAULT_LIMIT), mode = 'summary', pageFilter = undefined, rowclick, time, toolbarChildren }: Props = $props();
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
    const debouncedLoadData = debounce(10000, loadData);

    async function onPersistentEvent(message: WebSocketMessageValue<'PersistentEventChanged'>) {
        const shouldRefresh = () => {
            if (!filter) {
                return true;
            }

            const { id, organization_id, project_id, stack_id } = message;
            if (id) {
                // Check to see if any records on the page match
                if (mode === "summary" && table.options.data.some((doc) => doc.id === id)) {
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
                // Check to see if any records on the page match
                if (mode !== "summary" && table.options.data.some((doc) => doc.id === stack_id)) {
                    return true;
                }

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
                    if (message.id && mode === "summary") {
                        table.options.data = table.options.data.filter((doc) => doc.id !== message.id);
                    }

                    await debouncedLoadData();
                }

                break;
        }
    }

    useEventListener(document, 'refresh', async () => await loadData());
    useEventListener(document, 'PersistentEventChanged', async (event) => await onPersistentEvent((event as CustomEvent).detail));
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
