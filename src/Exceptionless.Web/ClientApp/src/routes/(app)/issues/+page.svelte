<script lang="ts">
    import type { EventSummaryModel, SummaryTemplateKeys } from '$features/events/components/summary/index';

    import { page } from '$app/state';
    import AutomaticRefreshIndicatorButton from '$comp/automatic-refresh-indicator-button.svelte';
    import * as DataTable from '$comp/data-table';
    import * as FacetedFilter from '$comp/faceted-filter';
    import { Button } from '$comp/ui/button';
    import * as Card from '$comp/ui/card';
    import * as Sheet from '$comp/ui/sheet';
    import { type GetEventsParams, getStackEventsQuery } from '$features/events/api.svelte';
    import EventsOverview from '$features/events/components/events-overview.svelte';
    import { type DateFilter, StatusFilter, TypeFilter } from '$features/events/components/filters';
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
    import EventsDataTable from '$features/events/components/table/events-data-table.svelte';
    import { getColumns } from '$features/events/components/table/options.svelte';
    import { organization } from '$features/organizations/context.svelte';
    import { getSharedTableOptions, isTableEmpty, removeTableData, removeTableSelection } from '$features/shared/table.svelte';
    import TableStacksBulkActionsDropdownMenu from '$features/stacks/components/stacks-bulk-actions-dropdown-menu.svelte';
    import { StackStatus } from '$features/stacks/models';
    import { ChangeType, type WebSocketMessageValue } from '$features/websockets/models';
    import { DEFAULT_LIMIT, useFetchClientStatus } from '$shared/api/api.svelte';
    import { type FetchClientResponse, useFetchClient } from '@exceptionless/fetchclient';
    import ExternalLink from '@lucide/svelte/icons/external-link';
    import { createTable } from '@tanstack/svelte-table';
    import { queryParamsState } from 'kit-query-params';
    import { useEventListener, watch } from 'runed';
    import { throttle } from 'throttle-debounce';

    // TODO: Update this page to use StackSummaryModel instead of EventSummaryModel.
    let selectedStackId = $state<string>();
    function rowclick(row: EventSummaryModel<SummaryTemplateKeys>) {
        selectedStackId = row.id;
    }

    // Load the latest event for the stack and display it in the sidebar.
    const eventsQuery = getStackEventsQuery({
        params: {
            limit: 1
        },
        route: {
            get stackId() {
                return selectedStackId;
            }
        }
    });
    const eventId = $derived(eventsQuery?.data?.[0]?.id);

    const DEFAULT_FILTERS = [new TypeFilter(['404', 'error']), new StatusFilter([StackStatus.Open, StackStatus.Regressed])];
    const DEFAULT_PARAMS = {
        filter: '(type:404 OR type:error) (status:open OR status:regressed)',
        limit: DEFAULT_LIMIT,
        time: 'last week'
    };

    function filterCacheKey(filter: null | string): string {
        return buildFilterCacheKey(organization.current, page.url.pathname, filter);
    }

    updateFilterCache(filterCacheKey(DEFAULT_PARAMS.filter), DEFAULT_FILTERS);
    const queryParams = queryParamsState({
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
            Object.assign(queryParams, DEFAULT_PARAMS);
        },
        { lazy: true }
    );

    let filters = $state(applyTimeFilter(getFiltersFromCache(filterCacheKey(queryParams.filter), queryParams.filter), queryParams.time));
    watch(
        [() => queryParams.filter, () => queryParams.time, () => filterCacheVersionNumber()],
        ([filter, time]) => {
            filters = applyTimeFilter(getFiltersFromCache(filterCacheKey(filter), filter), time);
        },
        { lazy: true }
    );

    $effect(() => {
        // Handle case where pop state loses the limit
        queryParams.limit ??= DEFAULT_LIMIT;
    });

    function onFilterChanged(addedOrUpdated: FacetedFilter.IFilter): void {
        updateFilters(filterChanged(filters ?? [], addedOrUpdated));
        selectedStackId = undefined;
    }

    function onFilterRemoved(removed?: FacetedFilter.IFilter): void {
        updateFilters(filterRemoved(filters ?? [], removed));
    }

    function updateFilters(updatedFilters: FacetedFilter.IFilter[]): void {
        const filter = toFilter(updatedFilters.filter((f) => f.type !== 'date'));

        updateFilterCache(filterCacheKey(filter), updatedFilters);
        queryParams.time = (updatedFilters.find((f) => f.type === 'date') as DateFilter)?.value as string;
        queryParams.filter = filter;
    }

    const eventsQueryParameters: GetEventsParams = $state({
        get filter() {
            return queryParams.filter!;
        },
        set filter(value) {
            queryParams.filter = value;
        },
        get limit() {
            return queryParams.limit!;
        },
        set limit(value) {
            queryParams.limit = value;
        },
        mode: 'stack_frequent'
    });

    const client = useFetchClient();
    const clientStatus = useFetchClientStatus(client);
    let clientResponse = $state<FetchClientResponse<EventSummaryModel<SummaryTemplateKeys>[]>>();

    const table = createTable(
        getSharedTableOptions<EventSummaryModel<SummaryTemplateKeys>>({
            columnPersistenceKey: 'issues-column-visibility',
            get columns() {
                return getColumns<EventSummaryModel<SummaryTemplateKeys>>(eventsQueryParameters.mode);
            },
            paginationStrategy: 'offset',
            get queryData() {
                return clientResponse?.data ?? [];
            },
            get queryMeta() {
                return clientResponse?.meta;
            },
            get queryParameters() {
                return eventsQueryParameters;
            }
        })
    );

    const canRefresh = $derived(!table.getIsSomeRowsSelected() && !table.getIsAllRowsSelected() && !table.getCanPreviousPage());

    async function loadData() {
        if (client.isLoading || !organization.current) {
            return;
        }

        clientResponse = await client.getJSON<EventSummaryModel<SummaryTemplateKeys>[]>(`organizations/${organization.current}/events`, {
            params: eventsQueryParameters as Record<string, unknown>
        });
    }

    const throttledLoadData = throttle(5000, loadData);

    async function onStackChanged(message: WebSocketMessageValue<'StackChanged'>) {
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
        if (!shouldRefreshPersistentEventChanged(filters, queryParams.filter, message.organization_id, message.project_id, message.id)) {
            return;
        }

        // Do not refresh if the grid has selections or grid is currently paged.
        if (!canRefresh) {
            return;
        }

        await throttledLoadData();
    }

    useEventListener(document, 'refresh', async () => await loadData());
    useEventListener(document, 'StackChanged', async (event) => await onStackChanged((event as CustomEvent).detail));

    $effect(() => {
        loadData();
    });
</script>

<div class="flex flex-col space-y-4">
    <EventsDataTable bind:limit={queryParams.limit!} isLoading={clientStatus.isLoading} rowClick={rowclick} {table}>
        {#snippet toolbarChildren()}
            <div class="text-lg font-medium pr-2">Issues</div>
            <FacetedFilter.Root changed={onFilterChanged} {filters} remove={onFilterRemoved}>
                <OrganizationDefaultsFacetedFilterBuilder />
            </FacetedFilter.Root>
        {/snippet}
        {#snippet footerChildren()}
            <div class="h-9 min-w-[140px]">
                <TableStacksBulkActionsDropdownMenu {table} />
            </div>

            <DataTable.PageSize bind:value={queryParams.limit!} {table}></DataTable.PageSize>
            <div class="flex items-center space-x-6 lg:space-x-8">
                <DataTable.PageCount {table} />
                <DataTable.Pagination {table} />
            </div>
        {/snippet}
    </EventsDataTable>
</div>

<Sheet.Root onOpenChange={() => (selectedStackId = undefined)} open={eventsQuery.isSuccess}>
    <Sheet.Content class="w-full overflow-y-auto sm:max-w-full md:w-5/6">
        <Sheet.Header>
            <Sheet.Title
                >Event Details <Button href="/next/event/{eventId}" size="sm" title="Open in new window" variant="ghost"><ExternalLink /></Button></Sheet.Title
            >
        </Sheet.Header>
        <div class="px-4">
            <EventsOverview filterChanged={onFilterChanged} id={eventId || ''} handleError={() => (selectedStackId = undefined)} />
        </div>
    </Sheet.Content>
</Sheet.Root>
