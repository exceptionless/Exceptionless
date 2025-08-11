<script lang="ts">
    import type { GetEventsParams } from '$features/events/api.svelte';
    import type { EventSummaryModel, SummaryTemplateKeys } from '$features/events/components/summary/index';

    import { page } from '$app/state';
    import * as DataTable from '$comp/data-table';
    import * as FacetedFilter from '$comp/faceted-filter';
    import StreamingIndicatorButton from '$comp/streaming-indicator-button.svelte';
    import { H3 } from '$comp/typography';
    import { Button } from '$comp/ui/button';
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
    import { getColumns } from '$features/events/components/table/options.svelte';
    import { organization } from '$features/organizations/context.svelte';
    import { getSharedTableOptions, isTableEmpty, removeTableData, removeTableSelection } from '$features/shared/table.svelte';
    import { StackStatus } from '$features/stacks/models';
    import { ChangeType, type WebSocketMessageValue } from '$features/websockets/models';
    import { DEFAULT_LIMIT, useFetchClientStatus } from '$shared/api/api.svelte';
    import { type FetchClientResponse, useFetchClient } from '@exceptionless/fetchclient';
    import ExternalLink from '@lucide/svelte/icons/external-link';
    import { createTable } from '@tanstack/svelte-table';
    import { queryParamsState } from 'kit-query-params';
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
            reset();
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
        selectedEventId = null;
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

    // TODO: Update all the query params as part of the query key.
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
        mode: 'summary',
        get time() {
            return queryParams.time!;
        },
        set time(value) {
            queryParams.time = value;
        }
    });

    const client = useFetchClient();
    const clientStatus = useFetchClientStatus(client);
    let clientResponse = $state<FetchClientResponse<EventSummaryModel<SummaryTemplateKeys>[]>>();

    const table = createTable(
        getSharedTableOptions<EventSummaryModel<SummaryTemplateKeys>>({
            columnPersistenceKey: 'events-column-visibility',
            get columns() {
                return getColumns<EventSummaryModel<SummaryTemplateKeys>>(eventsQueryParameters.mode);
            },
            paginationStrategy: 'cursor',
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

    const canRefresh = $derived(!table.getIsSomeRowsSelected() && !table.getIsAllRowsSelected() && table.getState().pagination.pageIndex === 0);

    let manualPause = $state(false);
    let paused = $derived(manualPause || !canRefresh);

    function reset() {
        manualPause = false;
        table.resetRowSelection();
        table.setPageIndex(0);
    }

    function handleToggle() {
        if (!canRefresh) {
            reset();
        } else {
            manualPause = !manualPause;
        }
    }

    async function loadData() {
        if (paused) {
            return;
        }

        if (client.isLoading || !organization.current) {
            return;
        }

        clientResponse = await client.getJSON<EventSummaryModel<SummaryTemplateKeys>[]>(`organizations/${organization.current}/events`, {
            params: eventsQueryParameters as Record<string, unknown>
        });
    }
    const throttledLoadData = throttle(10000, loadData);

    async function onPersistentEventChanged(message: WebSocketMessageValue<'PersistentEventChanged'>) {
        if (message.id && message.change_type === ChangeType.Removed) {
            removeTableSelection(table, message.id);

            if (removeTableData(table, (doc) => doc.id === message.id)) {
                // If the grid data is empty from all events being removed, we should refresh the data.
                if (isTableEmpty(table) && !paused) {
                    await throttledLoadData();
                    return;
                }
            }
        }

        if (paused) {
            return;
        }

        // Do not refresh if the filter criteria doesn't match the web socket message.
        if (
            !shouldRefreshPersistentEventChanged(filters ?? [], queryParams.filter, message.organization_id, message.project_id, message.stack_id, message.id)
        ) {
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
    <EventsDataTable bind:limit={queryParams.limit!} isLoading={clientStatus.isLoading} rowClick={rowclick} {table}>
        {#snippet toolbarChildren()}
            <H3 class="pr-2">Events</H3>
            <FacetedFilter.Root changed={onFilterChanged} {filters} remove={onFilterRemoved}>
                <OrganizationDefaultsFacetedFilterBuilder />
            </FacetedFilter.Root>
        {/snippet}
        {#snippet toolbarActions()}
            <StreamingIndicatorButton {paused} onToggle={handleToggle} />
        {/snippet}
        {#snippet footerChildren()}
            <div class="h-9 min-w-[140px]">
                {#if table.getSelectedRowModel().flatRows.length}
                    <EventsBulkActionsDropdownMenu {table} />
                {/if}
            </div>

            <DataTable.PageSize bind:value={queryParams.limit!} {table}></DataTable.PageSize>
            <div class="flex items-center space-x-6 lg:space-x-8">
                <DataTable.PageCount {table} />
                <DataTable.Pagination {table} />
            </div>
        {/snippet}
    </EventsDataTable>
</div>

<Sheet.Root onOpenChange={() => (selectedEventId = null)} open={!!selectedEventId}>
    <Sheet.Content class="w-full overflow-y-auto sm:max-w-full md:w-5/6">
        <Sheet.Header>
            <Sheet.Title
                >Event Details <Button href="/next/event/{selectedEventId}" size="sm" title="Open in new window" variant="ghost"><ExternalLink /></Button
                ></Sheet.Title
            >
        </Sheet.Header>
        <div class="px-4">
            <EventsOverview filterChanged={onFilterChanged} id={selectedEventId || ''} handleError={() => (selectedEventId = null)} />
        </div>
    </Sheet.Content>
</Sheet.Root>
