<script lang="ts">
    import type { GetEventsParams } from '$features/events/api.svelte';
    import type { EventSummaryModel, SummaryTemplateKeys } from '$features/events/components/summary/index';

    import { resolve } from '$app/paths';
    import { page } from '$app/state';
    import * as DataTable from '$comp/data-table';
    import DataTableViewOptions from '$comp/data-table/data-table-view-options.svelte';
    import * as FacetedFilter from '$comp/faceted-filter';
    import RefreshButton from '$comp/refresh-button.svelte';
    import { A, H3 } from '$comp/typography';
    import * as Alert from '$comp/ui/alert';
    import { Button } from '$comp/ui/button';
    import { Label } from '$comp/ui/label';
    import * as Sheet from '$comp/ui/sheet';
    import { Switch } from '$comp/ui/switch';
    import EventsOverview from '$features/events/components/events-overview.svelte';
    import { DateFilter, ProjectFilter, TypeFilter } from '$features/events/components/filters';
    import {
        applyTimeFilter,
        buildFilterCacheKey,
        filterCacheVersionNumber,
        filterChanged,
        filterRemoved,
        getFiltersFromCache,
        toFilter,
        updateFilterCache
    } from '$features/events/components/filters/helpers.svelte';
    import OrganizationDefaultsFacetedFilterBuilder from '$features/events/components/filters/organization-defaults-faceted-filter-builder.svelte';
    import EventsDataTable from '$features/events/components/table/events-data-table.svelte';
    import { getColumns } from '$features/events/components/table/options.svelte';
    import { getOrganizationQuery } from '$features/organizations/api.svelte';
    import { organization } from '$features/organizations/context.svelte';
    import { getOrganizationSessionsCountQuery } from '$features/sessions/api.svelte';
    import SessionsDashboardChart from '$features/sessions/components/sessions-dashboard-chart.svelte';
    import SessionsStatsDashboard from '$features/sessions/components/sessions-stats-dashboard.svelte';
    import * as agg from '$features/shared/api/aggregations';
    import { getSharedTableOptions, isTableEmpty, removeTableData, removeTableSelection } from '$features/shared/table.svelte';
    import { fillDateSeries } from '$features/shared/utils/charts.js';
    import { parseDateMathRange, toDateMathRange } from '$features/shared/utils/datemath';
    import { ChangeType, type WebSocketMessageValue } from '$features/websockets/models';
    import { DEFAULT_LIMIT, DEFAULT_OFFSET, useFetchClientStatus } from '$shared/api/api.svelte';
    import { type FetchClientResponse, useFetchClient } from '@exceptionless/fetchclient';
    import ExternalLink from '@lucide/svelte/icons/external-link';
    import InfoIcon from '@lucide/svelte/icons/info';
    import { createTable } from '@tanstack/svelte-table';
    import { queryParamsState } from 'kit-query-params';
    import { useEventListener, watch } from 'runed';
    import { throttle } from 'throttle-debounce';

    let selectedEventId: null | string = $state(null);
    function rowclick(row: EventSummaryModel<SummaryTemplateKeys>) {
        selectedEventId = row.id;
    }

    function rowHref(row: EventSummaryModel<SummaryTemplateKeys>): string {
        return resolve('/(app)/event/[eventId]', { eventId: row.id });
    }

    // Organization query to check premium features
    const organizationQuery = getOrganizationQuery({
        route: {
            get id() {
                return organization.current;
            }
        }
    });

    const hasPremiumFeatures = $derived(organizationQuery.data?.has_premium_features ?? false);

    // View Active toggle state
    let viewActive = $state(false);

    const DEFAULT_TIME_RANGE = '[now-7d TO now]';
    const DEFAULT_FILTERS = [new DateFilter('date', DEFAULT_TIME_RANGE), new ProjectFilter([]), new TypeFilter(['session'])];
    const DEFAULT_PARAMS = {
        filter: 'type:session',
        limit: DEFAULT_LIMIT,
        time: DEFAULT_TIME_RANGE
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

    // Build filter with active sessions toggle
    const activeFilter = $derived(() => {
        let filter = queryParams.filter ?? 'type:session';
        if (viewActive) {
            filter += ' _missing_:data.sessionend';
        }
        return filter;
    });

    const eventsQueryParameters: GetEventsParams = $state({
        get filter() {
            return activeFilter();
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
            columnPersistenceKey: 'sessions-column-visibility',
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
        if (client.isLoading || !organization.current) {
            return;
        }

        clientResponse = await client.getJSON<EventSummaryModel<SummaryTemplateKeys>[]>(`organizations/${organization.current}/events/sessions`, {
            params: eventsQueryParameters as Record<string, unknown>
        });
    }

    const throttledLoadData = throttle(10000, loadData);

    async function onPersistentEventChanged(message: WebSocketMessageValue<'PersistentEventChanged'>) {
        if (message.id && message.change_type === ChangeType.Removed) {
            removeTableSelection(table, message.id);

            if (removeTableData(table, (doc) => doc.id === message.id)) {
                if (isTableEmpty(table)) {
                    await throttledLoadData();
                    return;
                }
            }
        }
    }

    useEventListener(document, 'PersistentEventChanged', async (event) => await onPersistentEventChanged((event as CustomEvent).detail));

    $effect(() => {
        loadData();
    });

    // Session stats query with aggregations
    const statsQuery = getOrganizationSessionsCountQuery({
        params: {
            get aggregations() {
                return `avg:value cardinality:user date:(date${DEFAULT_OFFSET ? `^${DEFAULT_OFFSET}` : ''} cardinality:user)`;
            },
            get filter() {
                return activeFilter();
            },
            get time() {
                return eventsQueryParameters.time;
            }
        },
        route: { organizationId: organization.current }
    });

    // Compute stats from aggregations
    const stats = $derived.by(() => {
        if (!statsQuery.data?.aggregations) {
            return {
                avgDuration: 0,
                avgPerHour: 0,
                totalSessions: 0,
                totalUsers: 0
            };
        }

        const avgValue = agg.average(statsQuery.data.aggregations, 'avg_value')?.value ?? 0;
        const cardinalityUser = agg.cardinality(statsQuery.data.aggregations, 'cardinality_user')?.value ?? 0;
        const total = statsQuery.data.total ?? 0;

        // Calculate avg per hour based on time range
        const timeRange = parseDateMathRange(queryParams.time);
        const hours = (timeRange.end.getTime() - timeRange.start.getTime()) / (1000 * 60 * 60);
        const avgPerHour = hours > 0 ? total / hours : 0;

        return {
            avgDuration: avgValue,
            avgPerHour,
            totalSessions: total,
            totalUsers: cardinalityUser
        };
    });

    // Chart data from date histogram
    const chartData = $derived(() => {
        const timeRange = parseDateMathRange(queryParams.time);

        const buildZeroFilledSeries = () =>
            fillDateSeries(timeRange.start, timeRange.end, (date: Date) => ({
                date,
                sessions: 0,
                users: 0
            }));

        if (!statsQuery.data?.aggregations) {
            return buildZeroFilledSeries();
        }

        const dateHistogramBuckets = agg.dateHistogram(statsQuery.data.aggregations, 'date_date')?.buckets ?? [];
        if (dateHistogramBuckets.length === 0) {
            return buildZeroFilledSeries();
        }

        return dateHistogramBuckets.map((bucket) => ({
            date: new Date(bucket.key),
            sessions: bucket.total ?? 0,
            users: agg.cardinality(bucket.aggregations, 'cardinality_user')?.value ?? 0
        }));
    });

    function onRangeSelect(start: Date, end: Date) {
        onFilterChanged(new DateFilter('date', toDateMathRange(start, end)));
    }

    function handleUpgrade() {
        // Navigate to billing page
        if (organization.current) {
            window.location.href = resolve('/(app)/organization/[organizationId]/billing', { organizationId: organization.current });
        }
    }
</script>

<div class="flex flex-col">
    {#if organizationQuery.isSuccess && !hasPremiumFeatures}
        <Alert.Root variant="destructive" class="mb-4">
            <InfoIcon class="h-4 w-4" />
            <Alert.Title>Premium Feature</Alert.Title>
            <Alert.Description>
                <A onclick={handleUpgrade}>Upgrade now</A> to enable sessions and other premium features!
            </Alert.Description>
        </Alert.Root>
    {/if}

    <div class="mb-4 flex flex-wrap items-start gap-2">
        <H3 class="my-0 shrink-0">Sessions</H3>
        <div class="flex min-w-0 flex-1 flex-wrap items-start gap-2">
            <FacetedFilter.Root changed={onFilterChanged} {filters} remove={onFilterRemoved}>
                <OrganizationDefaultsFacetedFilterBuilder />
            </FacetedFilter.Root>
        </div>
        <div class="ml-auto flex shrink-0 items-center gap-2">
            <div class="flex items-center gap-2">
                <Switch id="view-active" bind:checked={viewActive} disabled={!hasPremiumFeatures} />
                <Label for="view-active" class="text-sm">View Active</Label>
            </div>
            <RefreshButton
                onRefresh={handleRefresh}
                isRefreshing={clientStatus.isLoading}
                size="icon-lg"
                title={canRefresh ? 'Refresh results' : 'Return to the first page to refresh results'}
            />
            <DataTableViewOptions size="icon-lg" {table} />
        </div>
    </div>

    <div class="flex flex-col gap-y-4" class:opacity-60={!hasPremiumFeatures}>
        <SessionsStatsDashboard
            avgDuration={stats.avgDuration}
            avgPerHour={stats.avgPerHour}
            isLoading={statsQuery.isLoading}
            totalSessions={stats.totalSessions}
            totalUsers={stats.totalUsers}
        />

        <SessionsDashboardChart data={chartData()} isLoading={clientStatus.isLoading || statsQuery.isLoading} {onRangeSelect} />

        <EventsDataTable bind:limit={queryParams.limit!} isLoading={clientStatus.isLoading} rowClick={rowclick} {rowHref} {table}>
            {#snippet footerChildren()}
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

<Sheet.Root onOpenChange={() => (selectedEventId = null)} open={!!selectedEventId}>
    <Sheet.Content class="w-full overflow-y-auto sm:max-w-full md:w-5/6">
        <Sheet.Header>
            <Sheet.Title
                >Event Details <Button
                    href={selectedEventId ? resolve('/(app)/event/[eventId]', { eventId: selectedEventId }) : '#'}
                    size="sm"
                    title="Open in new window"
                    variant="ghost"><ExternalLink /></Button
                ></Sheet.Title
            >
        </Sheet.Header>
        <div class="px-4">
            <EventsOverview filterChanged={onFilterChanged} id={selectedEventId || ''} handleError={() => (selectedEventId = null)} />
        </div>
    </Sheet.Content>
</Sheet.Root>
