<script lang="ts">
    import type { EventSummaryModel, SummaryTemplateKeys } from '$features/events/components/summary/index';

    import { page } from '$app/state';
    import AutomaticRefreshIndicatorButton from '$comp/automatic-refresh-indicator-button.svelte';
    import * as DataTable from '$comp/data-table';
    import * as FacetedFilter from '$comp/faceted-filter';
    import { Button } from '$comp/ui/button';
    import * as Card from '$comp/ui/card';
    import * as Sheet from '$comp/ui/sheet';
    import EventsOverview from '$features/events/components/events-overview.svelte';
    import { type DateFilter, StatusFilter } from '$features/events/components/filters';
    import {
        applyTimeFilter,
        buildFilterCacheKey,
        filterCacheVersionNumber,
        filterChanged,
        filterRemoved,
        getFiltersFromCache,
        shouldRefreshPersistentEventChanged,
        toFilter,
        updateFilterCache
    } from '$features/events/components/filters/helpers.svelte';
    import OrganizationDefaultsFacetedFilterBuilder from '$features/events/components/filters/organization-defaults-faceted-filter-builder.svelte';
    import EventsBulkActionsDropdownMenu from '$features/events/components/table/events-bulk-actions-dropdown-menu.svelte';
    import EventsDataTable from '$features/events/components/table/events-data-table.svelte';
    import { getTableContext } from '$features/events/components/table/options.svelte';
    import { organization } from '$features/organizations/context.svelte';
    import { isTableEmpty, removeTableData, removeTableSelection } from '$features/shared/table.svelte';
    import { StackStatus } from '$features/stacks/models';
    import { ChangeType, type WebSocketMessageValue } from '$features/websockets/models';
    import { DEFAULT_LIMIT, useFetchClientStatus } from '$shared/api/api.svelte';
    import { type FetchClientResponse, useFetchClient } from '@exceptionless/fetchclient';
    import { createTable } from '@tanstack/svelte-table';
    import { queryParamsState } from 'kit-query-params';
    import ExternalLink from 'lucide-svelte/icons/external-link';
    import { watch } from 'runed';
    import { useEventListener } from 'runed';
    import { throttle } from 'throttle-debounce';

    let selectedEventId: null | string = $state(null);
    function rowclick(row: EventSummaryModel<SummaryTemplateKeys>) {
        selectedEventId = row.id;
    }

    const DEFAULT_FILTERS = [new StatusFilter([StackStatus.Open, StackStatus.Regressed])];
    const DEFAULT_PARAMS = {
        filter: '(status:open OR status:regressed)',
        limit: DEFAULT_LIMIT,
        time: 'last week'
    };

    function filterCacheKey(filter: null | string): string {
        return buildFilterCacheKey(organization.current, page.url.pathname, filter);
    }

    updateFilterCache(filterCacheKey(DEFAULT_PARAMS.filter), DEFAULT_FILTERS);
    const params = queryParamsState({
        default: DEFAULT_PARAMS,
        pushHistory: true,
        schema: {
            filter: 'string',
            limit: 'number',
            time: 'string'
        }
    });

    watch(
        () => organization.current,
        () => {
            updateFilterCache(filterCacheKey(DEFAULT_PARAMS.filter), DEFAULT_FILTERS);
            //params.$reset(); // Work around for https://github.com/beynar/kit-query-params/issues/7
            Object.assign(params, DEFAULT_PARAMS);
        },
        { lazy: true }
    );

    let filters = $state(applyTimeFilter(getFiltersFromCache(filterCacheKey(params.filter), params.filter), params.time));
    watch(
        [() => params.filter, () => params.time, () => filterCacheVersionNumber()],
        ([filter, time]) => {
            filters = applyTimeFilter(getFiltersFromCache(filterCacheKey(filter), filter), time);
        },
        { lazy: true }
    );

    $effect(() => {
        // Handle case where pop state loses the limit
        params.limit ??= DEFAULT_LIMIT;
    });

    function onFilterChanged(addedOrUpdated: FacetedFilter.IFilter): void {
        updateFilters(filterChanged(filters ?? [], addedOrUpdated));
        selectedEventId = null;
    }

    function onFilterRemoved(removed?: FacetedFilter.IFilter): void {
        updateFilters(filterRemoved(filters ?? [], removed));
    }

    function updateFilters(updatedFilters: FacetedFilter.IFilter[]): void {
        const filter = toFilter(updatedFilters.filter((f) => f.type !== 'date'));

        updateFilterCache(filterCacheKey(filter), updatedFilters);
        params.time = (updatedFilters.find((f) => f.type === 'date') as DateFilter)?.value as string;
        params.filter = filter;
    }

    const context = getTableContext<EventSummaryModel<SummaryTemplateKeys>>({ limit: params.limit!, mode: 'summary' });
    const table = createTable(context.options);
    const canRefresh = $derived(!table.getIsSomeRowsSelected() && !table.getIsAllRowsSelected() && !table.getCanPreviousPage());

    const client = useFetchClient();
    const clientStatus = useFetchClientStatus(client);
    let clientResponse = $state<FetchClientResponse<EventSummaryModel<SummaryTemplateKeys>[]>>();

    async function loadData() {
        if (client.isLoading || !organization.current) {
            return;
        }

        clientResponse = await client.getJSON<EventSummaryModel<SummaryTemplateKeys>[]>(`organizations/${organization.current}/events`, {
            params: {
                ...context.parameters,
                filter: params.filter,
                time: params.time
            }
        });

        if (clientResponse.ok) {
            context.data = clientResponse.data || [];
            context.meta = clientResponse.meta;
        }
    }
    const throttledLoadData = throttle(10000, loadData);

    async function onPersistentEventChanged(message: WebSocketMessageValue<'PersistentEventChanged'>) {
        if (message.id && message.change_type === ChangeType.Removed) {
            removeTableSelection(table, message.id);

            if (removeTableData(table, (doc) => doc.id === message.id)) {
                // If the grid data is empty from all events being removed, we should refresh the data.
                if (isTableEmpty(table)) {
                    await throttledLoadData();
                    return;
                }
            }
        }

        // Do not refresh if the filter criteria doesn't match the web socket message.
        if (!shouldRefreshPersistentEventChanged(filters ?? [], params.filter, message.organization_id, message.project_id, message.stack_id, message.id)) {
            return;
        }

        // Do not refresh if the grid has selections or grid is currently paged.
        if (!canRefresh) {
            return;
        }

        await throttledLoadData();
    }

    useEventListener(document, 'refresh', () => loadData());
    useEventListener(document, 'PersistentEventChanged', async (event) => await onPersistentEventChanged((event as CustomEvent).detail));

    $effect(() => {
        loadData();
    });
</script>

<div class="flex flex-col space-y-4">
    <Card.Root>
        <Card.Header>
            <Card.Title class="text-2xl" level={2}
                >Events
                <AutomaticRefreshIndicatorButton {canRefresh} refresh={loadData} /></Card.Title
            ></Card.Header
        >
        <Card.Content class="pt-4">
            <EventsDataTable bind:limit={params.limit!} isLoading={clientStatus.isLoading} rowClick={rowclick} {table}>
                {#snippet toolbarChildren()}
                    <FacetedFilter.Root changed={onFilterChanged} {filters} remove={onFilterRemoved}>
                        <OrganizationDefaultsFacetedFilterBuilder />
                    </FacetedFilter.Root>
                {/snippet}
                {#snippet footerChildren()}
                    <div class="h-9 min-w-[140px]">
                        {#if table.getSelectedRowModel().flatRows.length}
                            <EventsBulkActionsDropdownMenu {table} />
                        {/if}
                    </div>

                    <DataTable.PageSize bind:value={params.limit!} {table}></DataTable.PageSize>
                    <div class="flex items-center space-x-6 lg:space-x-8">
                        <DataTable.PageCount {table} />
                        <DataTable.Pagination {table} />
                    </div>
                {/snippet}
            </EventsDataTable>
        </Card.Content>
    </Card.Root>
</div>

<Sheet.Root onOpenChange={() => (selectedEventId = null)} open={!!selectedEventId}>
    <Sheet.Content class="w-full overflow-y-auto sm:max-w-full md:w-5/6">
        <Sheet.Header>
            <Sheet.Title
                >Event Details <Button href="/next/event/{selectedEventId}" size="sm" title="Open in new window" variant="ghost"><ExternalLink /></Button
                ></Sheet.Title
            >
        </Sheet.Header>
        <EventsOverview filterChanged={onFilterChanged} id={selectedEventId || ''} handleError={() => (selectedEventId = null)}></EventsOverview>
    </Sheet.Content>
</Sheet.Root>
