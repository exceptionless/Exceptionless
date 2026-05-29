<script lang="ts">
    import type { GetEventsParams } from '$features/events/api.svelte';
    import type { EventSummaryModel, SummaryTemplateKeys } from '$features/events/components/summary/index';

    import { resolve } from '$app/paths';
    import { page } from '$app/state';
    import * as DataTable from '$comp/data-table';
    import * as FacetedFilter from '$comp/faceted-filter';
    import RefreshButton from '$comp/refresh-button.svelte';
    import { H3 } from '$comp/typography';
    import { showBillingDialogOnUpgradeProblem } from '$features/billing/upgrade-required.svelte';
    import { getOrganizationCountQuery } from '$features/events/api.svelte';
    import EventDetailSheet from '$features/events/components/event-detail-sheet.svelte';
    import EventsDashboardChart from '$features/events/components/events-dashboard-chart.svelte';
    import EventsStatsDashboard from '$features/events/components/events-stats-dashboard.svelte';
    import { DateFilter, ProjectFilter, StatusFilter } from '$features/events/components/filters';
    import {
        applyTimeFilter,
        buildFilterCacheKey,
        deserializeFilters,
        filterChanged,
        filterRemoved,
        getFiltersFromCache,
        hasSingleTypeFilter,
        serializeFilters,
        shouldRefreshPersistentEventChanged,
        toFilter,
        updateFilterCache
    } from '$features/events/components/filters/helpers.svelte';
    import OrganizationDefaultsFacetedFilterBuilder from '$features/events/components/filters/organization-defaults-faceted-filter-builder.svelte';
    import EventsBulkActionsDropdownMenu from '$features/events/components/table/events-bulk-actions-dropdown-menu.svelte';
    import EventsDataTable from '$features/events/components/table/events-data-table.svelte';
    import { defaultEventColumnVisibility, getColumns } from '$features/events/components/table/options.svelte';
    import { organization } from '$features/organizations/context.svelte';
    import SavedViewPicker from '$features/saved-views/components/saved-view-picker.svelte';
    import { useSavedViews } from '$features/saved-views/use-saved-views.svelte';
    import * as agg from '$features/shared/api/aggregations';
    import { getSharedTableOptions, isTableEmpty, removeTableData, removeTableSelection } from '$features/shared/table.svelte';
    import { fillDateSeries } from '$features/shared/utils/charts.js';
    import { toDateMathRange } from '$features/shared/utils/datemath';
    import { parseDateMathRange } from '$features/shared/utils/datemath.js';
    import { StackStatus } from '$features/stacks/models';
    import { ChangeType, type WebSocketMessageValue } from '$features/websockets/models';
    import { DEFAULT_LIMIT, DEFAULT_OFFSET, useFetchClientStatus } from '$shared/api/api.svelte';
    import { type FetchClientResponse, type ProblemDetails, useFetchClient } from '@exceptionless/fetchclient';
    import { createTable } from '@tanstack/svelte-table';
    import { queryParamsState } from 'kit-query-params';
    import { useEventListener, watch } from 'runed';
    import { debounce, throttle } from 'throttle-debounce';

    let selectedEventId: null | string = $state(null);

    function handleEventError(problem: ProblemDetails) {
        showBillingDialogOnUpgradeProblem(problem, organization.current);
        selectedEventId = null;
    }

    function rowClick(row: EventSummaryModel<SummaryTemplateKeys>) {
        selectedEventId = row.id;
    }

    function rowHref(row: EventSummaryModel<SummaryTemplateKeys>): string {
        return resolve('/(app)/events/[eventId=objectid]', { eventId: row.id });
    }

    const DEFAULT_TIME_RANGE = '[now-7d TO now]';
    const DEFAULT_FILTER = '(status:open OR status:regressed)';
    const DEFAULT_FILTERS = [new DateFilter('date', DEFAULT_TIME_RANGE), new ProjectFilter([]), new StatusFilter([StackStatus.Open, StackStatus.Regressed])];
    const DEFAULT_PARAMS = {
        filter: undefined as string | undefined,
        limit: DEFAULT_LIMIT,
        sort: undefined as string | undefined,
        time: undefined as string | undefined
    };

    function filterCacheKey(filter: null | string): string {
        return buildFilterCacheKey(organization.current, page.url.pathname, filter);
    }

    function getQueryTime(): null | string {
        if (queryParams.time != null) {
            return queryParams.time || null;
        }

        return savedViewsState.activeSavedView?.time ?? DEFAULT_TIME_RANGE;
    }

    function getEffectiveFilter(): null | string {
        if (queryParams.filter != null) {
            return queryParams.filter;
        }

        return savedViewsState.activeSavedView?.filter ?? DEFAULT_FILTER;
    }

    function getEffectiveSort(): null | string | undefined {
        if (queryParams.sort != null) {
            return queryParams.sort || undefined;
        }

        return savedViewsState.activeSavedView?.sort ?? undefined;
    }

    updateFilterCache(filterCacheKey(DEFAULT_FILTER), DEFAULT_FILTERS);
    const queryParams = queryParamsState({
        default: DEFAULT_PARAMS,
        pushHistory: true,
        schema: {
            filter: 'string',
            limit: 'number',
            sort: 'string',
            time: 'string'
        }
    });

    const VIEW = 'events';
    let showStats = $state(true);
    let showChart = $state(true);
    const savedViewsState = useSavedViews({
        baseHref: resolve('/(app)/events'),
        defaultColumnVisibility: defaultEventColumnVisibility,
        filterCacheKey,
        getColumnOrder: () => table.state.columnOrder,
        getColumnVisibility: () => table.state.columnVisibility,
        getFilter: getEffectiveFilter,
        getFilterDefinitions: () => serializeFilters(filters ?? []),
        getShowChart: () => showChart,
        getShowStats: () => showStats,
        getSort: getEffectiveSort,
        getTime: getQueryTime,
        queryParams,
        setColumnOrder: (v) => table.setColumnOrder(v),
        setColumnVisibility: (v) => table.setColumnVisibility(v),
        setShowChart: (v) => (showChart = v),
        setShowStats: (v) => (showStats = v),
        get slug() {
            return page.params.slug;
        },
        updateFilterCache,
        view: VIEW
    });
    const pageTitle = $derived(savedViewsState.activeSavedView?.name ?? 'Events');

    $effect(() => {
        document.title = `${pageTitle} - Exceptionless`;
    });

    // NOTE: This might be applying query string parameters when redirecting away.
    watch(
        () => organization.current,
        () => {
            updateFilterCache(filterCacheKey(DEFAULT_FILTER), DEFAULT_FILTERS);
            //params.$reset(); // Work around for https://github.com/beynar/kit-query-params/issues/7
            Object.assign(queryParams, DEFAULT_PARAMS);
            reset();
        },
        { lazy: true }
    );

    function getCurrentFilters(): FacetedFilter.IFilter[] {
        const filter = getEffectiveFilter();
        const savedView = savedViewsState.activeSavedView;

        if (queryParams.filter == null && savedView?.filter_definitions && filter === (savedView.filter ?? null)) {
            const hydrated = deserializeFilters(savedView.filter_definitions);
            return applyTimeFilter(hydrated, getQueryTime());
        }

        return applyTimeFilter(getFiltersFromCache(filterCacheKey(filter), filter), getQueryTime());
    }

    let filters = $state(getCurrentFilters());
    let isInternalFilterUpdate = false;
    watch(
        [() => page.url.pathname, () => page.url.search, () => savedViewsState.activeSavedView],
        () => {
            if (isInternalFilterUpdate) {
                isInternalFilterUpdate = false;
                return;
            }

            filters = getCurrentFilters();
        },
        { lazy: true }
    );

    $effect(() => {
        // Handle case where pop state loses the limit
        queryParams.limit ??= DEFAULT_LIMIT;
    });

    function onFilterChanged(addedOrUpdated: FacetedFilter.IFilter): void {
        const isNew = !filters?.some((f) => f.id === addedOrUpdated.id);
        const updatedFilters = filterChanged(filters ?? [], addedOrUpdated);
        updateFilters(updatedFilters);
        if (isNew) {
            filters = updatedFilters;
        }

        selectedEventId = null;
    }

    function onFilterRemoved(removed?: FacetedFilter.IFilter): void {
        const updatedFilters = filterRemoved(filters ?? [], removed);
        updateFilters(updatedFilters);
        filters = updatedFilters;
    }

    function updateFilters(updatedFilters: FacetedFilter.IFilter[]): void {
        const filter = toFilter(updatedFilters.filter((f) => f.type !== 'date'));
        const time = ((updatedFilters.find((f) => f.type === 'date') as DateFilter | undefined)?.value as string | undefined) ?? null;
        const baseFilter = savedViewsState.activeSavedView?.filter ?? DEFAULT_FILTER;
        const baseTime = savedViewsState.activeSavedView?.time ?? DEFAULT_TIME_RANGE;

        const newFilterParam = filter === baseFilter ? null : filter;
        const newTimeParam = time === baseTime ? null : (time ?? '');

        updateFilterCache(filterCacheKey(filter), updatedFilters);
        // Only skip the watch when the URL will actually change from our update.
        // If the URL doesn't change, the watch won't fire and the flag would stay stale.
        if (newFilterParam !== queryParams.filter || newTimeParam !== queryParams.time) {
            isInternalFilterUpdate = true;
        }

        queryParams.time = newTimeParam;
        queryParams.filter = newFilterParam;
    }

    const eventsQueryParameters: GetEventsParams = $state({
        get filter() {
            return getEffectiveFilter()!;
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
        offset: DEFAULT_OFFSET,
        get sort() {
            return getEffectiveSort() ?? undefined;
        },
        set sort(value) {
            const baseSort = savedViewsState.activeSavedView?.sort ?? undefined;
            queryParams.sort = value === baseSort ? null : (value ?? null);
        },
        get time() {
            return getQueryTime() ?? undefined;
        },
        set time(value) {
            queryParams.time = value ?? null;
        }
    });

    const client = useFetchClient();
    const clientStatus = useFetchClientStatus(client);
    let clientResponse = $state<FetchClientResponse<EventSummaryModel<SummaryTemplateKeys>[]>>();

    const table = createTable(
        getSharedTableOptions<EventSummaryModel<SummaryTemplateKeys>>({
            columnPersistenceKey: 'events-column-visibility',
            get columns() {
                return getColumns<EventSummaryModel<SummaryTemplateKeys>>(eventsQueryParameters.mode, {
                    showType: !hasSingleTypeFilter(eventsQueryParameters.filter)
                });
            },
            defaultColumnVisibility: defaultEventColumnVisibility,
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
        }),
        (state) => ({
            columnOrder: state.columnOrder,
            columnVisibility: state.columnVisibility,
            pagination: state.pagination
        })
    );

    const canRefresh = $derived(!table.getIsSomeRowsSelected() && !table.getIsAllRowsSelected() && table.state.pagination.pageIndex === 0);

    function reset() {
        table.resetRowSelection();
        table.setPageIndex(0);
    }

    async function handleRefresh() {
        if (!canRefresh) {
            reset();
        }

        await loadData();
    }

    async function loadData() {
        if (!organization.current) {
            return;
        }

        clientResponse = await client.getJSON<EventSummaryModel<SummaryTemplateKeys>[]>(`organizations/${organization.current}/events`, {
            params: eventsQueryParameters as Record<string, unknown>
        });

        if (clientResponse.problem) {
            showBillingDialogOnUpgradeProblem(clientResponse.problem, organization.current, () => loadData());
        }
    }

    const throttledLoadData = throttle(10000, loadData);
    const debouncedLoadData = debounce(1500, loadData);

    async function onPersistentEventChanged(message: WebSocketMessageValue<'PersistentEventChanged'>) {
        if (message.id && message.change_type === ChangeType.Removed) {
            removeTableSelection(table, message.id);

            if (removeTableData(table, (doc: EventSummaryModel<SummaryTemplateKeys>) => doc.id === message.id)) {
                // If the grid data is empty from all events being removed, we should refresh the data.
                if (isTableEmpty(table)) {
                    await throttledLoadData();
                    return;
                }
            }
        }

        if (message.change_type === ChangeType.Removed) {
            return;
        }

        if (!shouldRefreshPersistentEventChanged(filters, queryParams.filter, message.organization_id, message.project_id, message.stack_id, message.id)) {
            return;
        }

        await debouncedLoadData();
    }

    useEventListener(document, 'PersistentEventChanged', async (event) => await onPersistentEventChanged((event as CustomEvent).detail));

    $effect(() => {
        loadData();
    });

    const chartDataQuery = getOrganizationCountQuery({
        params: {
            get aggregations() {
                return `date:(date${DEFAULT_OFFSET ? `^${DEFAULT_OFFSET}` : ''} cardinality:stack sum:count~1) cardinality:stack terms:(first @include:true) sum:count~1`;
            },
            get filter() {
                return eventsQueryParameters.filter;
            },
            get time() {
                return eventsQueryParameters.time;
            }
        },
        route: {
            get organizationId() {
                return organization.current;
            }
        }
    });

    const chartData = $derived(() => {
        const timeRange = parseDateMathRange(getQueryTime() || undefined);
        const buildZeroFilledSeries = () =>
            fillDateSeries(timeRange.start, timeRange.end, (date: Date) => ({
                date,
                events: 0,
                stacks: 0
            }));

        if (!chartDataQuery.data?.aggregations) {
            return buildZeroFilledSeries();
        }

        const dateHistogramBuckets = agg.dateHistogram(chartDataQuery.data.aggregations, 'date_date')?.buckets ?? [];
        if (dateHistogramBuckets.length === 0) {
            return buildZeroFilledSeries();
        }

        return dateHistogramBuckets.map((bucket) => ({
            date: new Date(bucket.key),
            events: agg.sum(bucket.aggregations, 'sum_count')?.value ?? 0,
            stacks: agg.cardinality(bucket.aggregations, 'cardinality_stack')?.value ?? 0
        }));
    });

    const stats = $derived.by(() => {
        const aggregations = chartDataQuery.data?.aggregations;
        const timeRange = parseDateMathRange(getQueryTime() || undefined);
        const totalEvents = agg.sum(aggregations, 'sum_count')?.value ?? chartDataQuery.data?.total ?? 0;
        const totalStacks = agg.cardinality(aggregations, 'cardinality_stack')?.value ?? 0;
        const newStacks = agg.terms<boolean>(aggregations, 'terms_first')?.buckets[0]?.total ?? 0;
        const hours = Math.max((timeRange.end.getTime() - timeRange.start.getTime()) / 3_600_000, 1);

        return {
            eventsPerHour: totalEvents / hours,
            newStacks,
            totalEvents,
            totalStacks
        };
    });

    let lastStatsRefreshKey = $state<string>();

    $effect(() => {
        const refreshKey = `${organization.current}:${page.url.search}:${stats.totalEvents}`;

        if (!clientResponse?.ok || stats.totalEvents <= 0 || !isTableEmpty(table) || lastStatsRefreshKey === refreshKey) {
            return;
        }

        lastStatsRefreshKey = refreshKey;
        void loadData();
    });

    function onRangeSelect(start: Date, end: Date) {
        onFilterChanged(new DateFilter('date', toDateMathRange(start, end)));
    }
</script>

<div class="flex flex-col">
    <div class="mb-4 flex flex-wrap items-start gap-2">
        <H3 class="my-0 shrink-0">{pageTitle}</H3>
        <div class="flex min-w-0 flex-1 flex-wrap items-start gap-2">
            <FacetedFilter.Root changed={onFilterChanged} {filters} remove={onFilterRemoved}>
                <OrganizationDefaultsFacetedFilterBuilder />
            </FacetedFilter.Root>
        </div>
        <div class="ml-auto flex shrink-0 items-start gap-2">
            {#if savedViewsState.isEnabled}
                <SavedViewPicker
                    activeSavedView={savedViewsState.activeSavedView}
                    columnOrder={table.state.columnOrder}
                    columnVisibility={table.state.columnVisibility}
                    filters={filters ?? []}
                    isModified={savedViewsState.isModified}
                    onLoadView={savedViewsState.handleLoadView}
                    onClearSavedView={savedViewsState.handleClearSavedView}
                    onResetToSaved={savedViewsState.handleResetToSaved}
                    savedViews={savedViewsState.savedViews}
                    {showChart}
                    {showStats}
                    setShowChart={(v) => (showChart = v)}
                    setShowStats={(v) => (showStats = v)}
                    sort={getEffectiveSort() ?? undefined}
                    {table}
                    time={getQueryTime() ?? undefined}
                    view={VIEW}
                />
            {/if}
            <RefreshButton
                onRefresh={handleRefresh}
                isRefreshing={clientStatus.isLoading}
                size="icon-lg"
                title={canRefresh ? 'Refresh results' : 'Return to the first page to refresh results'}
            />
        </div>
    </div>

    <div class="flex flex-col gap-y-4">
        {#if showStats}
            <EventsStatsDashboard
                eventsPerHour={stats.eventsPerHour}
                isLoading={chartDataQuery.isLoading && !chartDataQuery.isSuccess}
                newStacks={stats.newStacks}
                totalEvents={stats.totalEvents}
                totalStacks={stats.totalStacks}
            />
        {/if}

        {#if showChart}
            <EventsDashboardChart data={chartData()} isLoading={chartDataQuery.isLoading && !chartDataQuery.isSuccess} {onRangeSelect} />
        {/if}

        <EventsDataTable bind:limit={queryParams.limit!} isLoading={clientStatus.isLoading} {rowClick} {rowHref} {table}>
            {#snippet footerChildren()}
                <div class="h-9 min-w-35">
                    {#if table.getSelectedRowModel().flatRows.length}
                        <EventsBulkActionsDropdownMenu {table} />
                    {/if}
                </div>

                <DataTable.Selection {table} />
                <DataTable.PageSize bind:value={queryParams.limit!} {table}></DataTable.PageSize>
                <div class="flex items-center space-x-6 lg:space-x-8">
                    <DataTable.PageCount {table} />
                    <DataTable.Pagination {table} />
                </div>
            {/snippet}
        </EventsDataTable>
    </div>
</div>

<EventDetailSheet eventId={selectedEventId} filterChanged={onFilterChanged} onClose={() => (selectedEventId = null)} onError={handleEventError} />
