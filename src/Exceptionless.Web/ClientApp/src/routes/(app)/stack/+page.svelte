<script lang="ts">
    import type { EventSummaryModel, SummaryTemplateKeys } from '$features/events/components/summary/index';

    import { resolve } from '$app/paths';
    import { page } from '$app/state';
    import * as DataTable from '$comp/data-table';
    import * as FacetedFilter from '$comp/faceted-filter';
    import RefreshButton from '$comp/refresh-button.svelte';
    import { H3 } from '$comp/typography';
    import { showBillingDialogOnUpgradeProblem } from '$features/billing/upgrade-required.svelte';
    import { type GetEventsParams, getOrganizationCountQuery } from '$features/events/api.svelte';
    import EventsDashboardChart from '$features/events/components/events-dashboard-chart.svelte';
    import EventsStatsDashboard from '$features/events/components/events-stats-dashboard.svelte';
    import {
        BooleanFilter,
        DateFilter,
        LevelFilter,
        ProjectFilter,
        ReferenceFilter,
        SessionFilter,
        StatusFilter,
        StringFilter,
        TagFilter,
        TypeFilter,
        VersionFilter
    } from '$features/events/components/filters';
    import {
        applyTimeFilter,
        buildFilterCacheKey,
        deserializeFilters,
        filterChanged,
        filterRemoved,
        getFiltersFromCache,
        hasSingleTypeFilter,
        serializeFilters,
        toFilter,
        updateFilterCache
    } from '$features/events/components/filters/helpers.svelte';
    import OrganizationDefaultsFacetedFilterBuilder from '$features/events/components/filters/organization-defaults-faceted-filter-builder.svelte';
    import EventsDataTable from '$features/events/components/table/events-data-table.svelte';
    import { getColumns } from '$features/events/components/table/options.svelte';
    import { organization } from '$features/organizations/context.svelte';
    import SavedViewPicker from '$features/saved-views/components/saved-view-picker.svelte';
    import { useSavedViews } from '$features/saved-views/use-saved-views.svelte';
    import * as agg from '$features/shared/api/aggregations';
    import { createPageSizePreference, getSharedTableOptions, isTableEmpty, removeTableData, removeTableSelection } from '$features/shared/table.svelte';
    import { fillDateSeries } from '$features/shared/utils/charts';
    import { parseDateMathRange, toDateMathRange } from '$features/shared/utils/datemath';
    import StackDetailSheet from '$features/stacks/components/stack-detail-sheet.svelte';
    import TableStacksBulkActionsDropdownMenu from '$features/stacks/components/stacks-bulk-actions-dropdown-menu.svelte';
    import { StackStatus } from '$features/stacks/models';
    import { ChangeType, type WebSocketMessageValue } from '$features/websockets/models';
    import { DEFAULT_OFFSET, useFetchClientStatus } from '$shared/api/api.svelte';
    import { type FetchClientResponse, type ProblemDetails, useFetchClient } from '@exceptionless/fetchclient';
    import { error } from '@sveltejs/kit';
    import { createTable } from '@tanstack/svelte-table';
    import { queryParamsState } from 'kit-query-params';
    import { useEventListener, watch } from 'runed';
    import { untrack } from 'svelte';
    import { throttle } from 'throttle-debounce';

    import {
        ALL_TIME_QUERY_VALUE,
        clearListFilterQueryParams,
        deserializeTimeQueryParam,
        getEventsNavigationOptionsForFilter,
        getListFilterQueryParams,
        type ListFilterQueryParams,
        redirectToEventsWithFilter,
        serializeTimeQueryParam
    } from '../redirect-to-events.svelte';

    // TODO: Update this page to use StackSummaryModel instead of EventSummaryModel.
    let selectedStackId = $state<string>();
    const DEFAULT_FILTER = '(type:404 OR type:error) (status:open OR status:regressed)';

    function handleStackError(problem: ProblemDetails) {
        showBillingDialogOnUpgradeProblem(problem, organization.current);
        selectedStackId = undefined;
    }

    function rowClick(row: EventSummaryModel<SummaryTemplateKeys>) {
        selectedStackId = row.id;
    }

    function rowHref(row: EventSummaryModel<SummaryTemplateKeys>): string {
        return resolve('/(app)/stack/[stackId=objectid]', { stackId: row.id });
    }

    const DEFAULT_TIME_RANGE = '[now-7d TO now]';
    const DEFAULT_FILTERS = [
        new DateFilter('date', DEFAULT_TIME_RANGE),
        new ProjectFilter([]),
        new TypeFilter(['404', 'error']),
        new StatusFilter([StackStatus.Open, StackStatus.Regressed])
    ];
    const PAGE_SIZE_PREFERENCE_KEY = 'event-stack-list-page-size';
    const pageSizePreference = createPageSizePreference(PAGE_SIZE_PREFERENCE_KEY);
    const DEFAULT_PARAMS = {
        bot: undefined as string | undefined,
        filter: undefined as string | undefined,
        first: undefined as string | undefined,
        level: undefined as string | undefined,
        limit: undefined as number | undefined,
        page: undefined as number | undefined,
        project: undefined as string | undefined,
        reference: undefined as string | undefined,
        session: undefined as string | undefined,
        stack: undefined as string | undefined,
        status: undefined as string | undefined,
        tag: undefined as string | undefined,
        time: undefined as string | undefined,
        type: undefined as string | undefined,
        version: undefined as string | undefined
    };

    function filterCacheKey(filter: null | string): string {
        return buildFilterCacheKey(organization.current, page.url.pathname, filter);
    }

    function getQueryTime(params: ListFilterQueryParams = queryParams): null | string {
        if (params.time != null) {
            if (params.time === ALL_TIME_QUERY_VALUE) {
                return null;
            }

            return params.time ? deserializeTimeQueryParam(params.time) : null;
        }

        return savedViewsState.activeSavedView?.time ?? DEFAULT_TIME_RANGE;
    }

    function getEffectiveFilter(): null | string {
        const filter = toFilter(getCurrentFiltersWithoutTime());
        return filter || null;
    }

    function getQueryFilters(params: ListFilterQueryParams = queryParams): FacetedFilter.IFilter[] | null {
        const filters: FacetedFilter.IFilter[] = [];

        if (params.project) {
            filters.push(new ProjectFilter(splitQueryParam(params.project)));
        }

        if (params.stack) {
            filters.push(new StringFilter('stack', params.stack));
        }

        const bot = parseBooleanQueryParam(params.bot);
        if (bot !== undefined) {
            filters.push(new BooleanFilter('bot', bot));
        }

        const first = parseBooleanQueryParam(params.first);
        if (first !== undefined) {
            filters.push(new BooleanFilter('first', first));
        }

        if (params.level) {
            filters.push(new LevelFilter(splitQueryParam(params.level) as never[]));
        }

        if (params.reference) {
            filters.push(new ReferenceFilter(params.reference));
        }

        if (params.session) {
            filters.push(new SessionFilter(params.session));
        }

        if (params.status) {
            filters.push(new StatusFilter(splitQueryParam(params.status) as never[]));
        }

        if (params.tag) {
            filters.push(new TagFilter(splitQueryParam(params.tag) as never[]));
        }

        if (params.type) {
            filters.push(new TypeFilter(splitQueryParam(params.type) as never[]));
        }

        if (params.version) {
            filters.push(new VersionFilter('version', params.version));
        }

        return filters.length > 0 ? filters : null;
    }

    function parseBooleanQueryParam(value: null | string | undefined): boolean | undefined {
        if (value === 'true') {
            return true;
        }

        if (value === 'false') {
            return false;
        }

        return undefined;
    }

    function splitQueryParam(value: string): string[] {
        return value
            .split(',')
            .map((item) => item.trim())
            .filter((item) => item);
    }

    updateFilterCache(filterCacheKey(DEFAULT_FILTER), DEFAULT_FILTERS);
    const queryParams = queryParamsState({
        default: DEFAULT_PARAMS,
        pushHistory: true,
        schema: {
            bot: 'string',
            filter: 'string',
            first: 'string',
            level: 'string',
            limit: 'number',
            page: 'number',
            project: 'string',
            reference: 'string',
            session: 'string',
            stack: 'string',
            status: 'string',
            tag: 'string',
            time: 'string',
            type: 'string',
            version: 'string'
        }
    });

    const VIEW = 'stacks';
    let showStats = $state(true);
    let showChart = $state(true);
    const savedViewsState = useSavedViews({
        baseHref: resolve('/(app)/stack'),
        defaultFilter: DEFAULT_FILTER,
        defaultTime: DEFAULT_TIME_RANGE,
        filterCacheKey,
        getColumnOrder: () => table.state.columnOrder,
        getColumnVisibility: () => table.state.columnVisibility,
        getFilter: getEffectiveFilter,
        getFilterDefinitions: () => serializeFilters(filters ?? []),
        getShowChart: () => showChart,
        getShowStats: () => showStats,
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
    const pageTitle = $derived(savedViewsState.activeSavedView?.name ?? 'Stacks');
    const isSavedViewRoutePending = $derived(!!page.params.slug && !savedViewsState.activeSavedView);

    $effect(() => {
        document.title = `${pageTitle} - Exceptionless`;
    });

    function throwSavedViewNotFound(): never {
        throw error(404, `The saved Stacks view "${page.params.slug}" could not be found.`);
    }

    watch(
        () => organization.current,
        (_currentOrganizationId, previousOrganizationId) => {
            if (previousOrganizationId === undefined) {
                return;
            }

            updateFilterCache(filterCacheKey(DEFAULT_FILTER), DEFAULT_FILTERS);
            //params.$reset(); // Work around for https://github.com/beynar/kit-query-params/issues/7
            Object.assign(queryParams, DEFAULT_PARAMS);
            reset();
        },
        { lazy: true }
    );

    function getCurrentFilters(params: ListFilterQueryParams = queryParams): FacetedFilter.IFilter[] {
        return applyTimeFilter(getCurrentFiltersWithoutTime(params), getQueryTime(params));
    }

    function getCurrentFiltersWithoutTime(params: ListFilterQueryParams = queryParams): FacetedFilter.IFilter[] {
        const savedViewFilters = getSavedViewFilters();
        const queryFilters = getQueryFilters(params) ?? [];
        const expressionFilters =
            params.filter != null ? getFiltersFromCache(filterCacheKey(params.filter), params.filter).filter((filter) => filter.type !== 'date') : [];

        if (savedViewFilters) {
            return mergeFilterOverrides(
                savedViewFilters.filter((filter) => filter.type !== 'date'),
                [...expressionFilters, ...queryFilters],
                getQueryFilterRemovalKeys(savedViewFilters, params)
            );
        }

        if (expressionFilters.length > 0 || queryFilters.length > 0) {
            return [...expressionFilters, ...queryFilters];
        }

        const filter = savedViewsState.activeSavedView?.filter ?? DEFAULT_FILTER;
        return getFiltersFromCache(filterCacheKey(filter), filter).filter((filter) => filter.type !== 'date');
    }

    function getSavedViewFilters(): FacetedFilter.IFilter[] | null {
        const savedView = savedViewsState.activeSavedView;
        if (!savedView?.filter_definitions) {
            return null;
        }

        return deserializeFilters(savedView.filter_definitions);
    }

    function getQueryFilterRemovalKeys(savedViewFilters: FacetedFilter.IFilter[], params: ListFilterQueryParams = queryParams): string[] {
        const removedKeys: string[] = [];

        if (params.bot === '') {
            removedKeys.push('boolean-bot');
        }

        if (params.first === '') {
            removedKeys.push('boolean-first');
        }

        if (params.filter === '') {
            removedKeys.push(...savedViewFilters.filter((filter) => filter.type !== 'date' && !isQueryParamFilter(filter)).map((filter) => filter.key));
        }

        if (params.level === '') {
            removedKeys.push('level');
        }

        if (params.project === '') {
            removedKeys.push('project');
        }

        if (params.reference === '') {
            removedKeys.push('reference');
        }

        if (params.session === '') {
            removedKeys.push('session');
        }

        if (params.stack === '') {
            removedKeys.push('string-stack');
        }

        if (params.status === '') {
            removedKeys.push('status');
        }

        if (params.tag === '') {
            removedKeys.push('tag');
        }

        if (params.type === '') {
            removedKeys.push('type');
        }

        if (params.version === '') {
            removedKeys.push('version-version');
        }

        return removedKeys;
    }

    function mergeFilterOverrides(
        baseFilters: FacetedFilter.IFilter[],
        overrideFilters: FacetedFilter.IFilter[],
        removedFilterKeys: string[] = []
    ): FacetedFilter.IFilter[] {
        if (overrideFilters.length === 0 && removedFilterKeys.length === 0) {
            return baseFilters;
        }

        const overrideKeys = new Set([...overrideFilters.map((filter) => filter.key), ...removedFilterKeys]);
        return [...baseFilters.filter((filter) => !overrideKeys.has(filter.key)), ...overrideFilters];
    }

    let filters = $state(getCurrentFilters());
    let isInternalFilterUpdate = false;
    watch(
        [() => page.url.pathname, () => page.url.search, () => savedViewsState.activeSavedView],
        ([pathname, , activeSavedView], [previousPathname, , previousSavedView]) => {
            const savedViewChanged = pathname !== previousPathname || activeSavedView?.id !== previousSavedView?.id;
            if (isInternalFilterUpdate && !savedViewChanged) {
                isInternalFilterUpdate = false;
                return;
            }

            isInternalFilterUpdate = false;
            filters = getCurrentFilters(getListFilterQueryParams(page.url.searchParams));
        },
        { lazy: true }
    );

    function handleResetToSaved(): void {
        isInternalFilterUpdate = false;
        clearListFilterQueryParams(queryParams);
        savedViewsState.handleResetToSaved();
        filters = getCurrentFilters();
    }

    async function onFilterChanged(addedOrUpdated: FacetedFilter.IFilter) {
        // If this is a stack filter, redirect to the Events page
        if (addedOrUpdated.type === 'string' && addedOrUpdated.key === 'string-stack') {
            await redirectToEventsWithFilter(organization.current, addedOrUpdated, getEventsNavigationOptionsForFilter(addedOrUpdated));
            return;
        }

        // For all other filters, apply them to the current page
        const isNew = !filters?.some((f) => f.id === addedOrUpdated.id);
        const updatedFilters = filterChanged(filters ?? [], addedOrUpdated);
        updateFilters(updatedFilters);
        // Only reassign filters for newly added filters to avoid re-rendering open popovers.
        // Existing filter values are already mutated in place via $state.
        if (isNew) {
            filters = updatedFilters;
        }

        selectedStackId = undefined;
    }

    function onFilterRemoved(removed?: FacetedFilter.IFilter): void {
        const updatedFilters = filterRemoved(filters ?? [], removed);
        updateFilters(updatedFilters);
        filters = updatedFilters;
    }

    function updateFilters(updatedFilters: FacetedFilter.IFilter[], options: { clearPagination?: boolean } = {}): void {
        const shouldClearPagination = options.clearPagination ?? true;
        const filter = toFilter(updatedFilters.filter((f) => f.type !== 'date'));
        const expressionFilters = updatedFilters.filter((f) => f.type !== 'date' && !isQueryParamFilter(f));
        const filterParam = toFilter(expressionFilters);
        const time = ((updatedFilters.find((f) => f.type === 'date') as DateFilter | undefined)?.value as string | undefined) ?? null;
        const baseTime = savedViewsState.activeSavedView?.time ?? DEFAULT_TIME_RANGE;
        const baseFilter = savedViewsState.activeSavedView?.filter ?? DEFAULT_FILTER;
        const savedViewFilters = getSavedViewFilters();
        const baseQueryFilterParams = getQueryFilterParams(savedViewFilters ?? []);
        const queryFilterParams = getQueryFilterParamDeltas(getQueryFilterParams(updatedFilters), baseQueryFilterParams);
        const baseExpressionFilterParam = savedViewFilters ? toFilter(savedViewFilters.filter((f) => f.type !== 'date' && !isQueryParamFilter(f))) : baseFilter;

        const newFilterParam = filterParam === baseExpressionFilterParam ? null : filterParam || (savedViewFilters && baseExpressionFilterParam ? '' : null);
        const newTimeParam = time === baseTime ? null : time ? serializeTimeQueryParam(time) : ALL_TIME_QUERY_VALUE;
        const urlQueryWillChange =
            newFilterParam !== queryParams.filter ||
            newTimeParam !== queryParams.time ||
            queryFilterParams.bot !== queryParams.bot ||
            queryFilterParams.first !== queryParams.first ||
            queryFilterParams.level !== queryParams.level ||
            queryFilterParams.project !== queryParams.project ||
            queryFilterParams.reference !== queryParams.reference ||
            queryFilterParams.session !== queryParams.session ||
            queryFilterParams.stack !== queryParams.stack ||
            queryFilterParams.status !== queryParams.status ||
            queryFilterParams.tag !== queryParams.tag ||
            queryFilterParams.type !== queryParams.type ||
            queryFilterParams.version !== queryParams.version;
        const effectiveQueryWillChange = (filter || null) !== getEffectiveFilter() || time !== getQueryTime();
        const shouldClearPaginationForFilter = shouldClearPagination && effectiveQueryWillChange;
        const paginationWillChange = shouldClearPaginationForFilter && queryParams.page != null;

        updateFilterCache(filterCacheKey(filter), updatedFilters);
        if (shouldClearPaginationForFilter) {
            clearPaginationQueryParams();
        }

        // Only skip the watch when the URL will actually change from our update.
        // If the URL doesn't change, the watch won't fire and the flag would stay stale.
        if (paginationWillChange || urlQueryWillChange) {
            isInternalFilterUpdate = true;
        }

        queryParams.bot = queryFilterParams.bot;
        queryParams.first = queryFilterParams.first;
        queryParams.level = queryFilterParams.level;
        queryParams.project = queryFilterParams.project;
        queryParams.reference = queryFilterParams.reference;
        queryParams.session = queryFilterParams.session;
        queryParams.stack = queryFilterParams.stack;
        queryParams.status = queryFilterParams.status;
        queryParams.tag = queryFilterParams.tag;
        queryParams.type = queryFilterParams.type;
        queryParams.version = queryFilterParams.version;
        queryParams.time = newTimeParam;
        queryParams.filter = newFilterParam;
    }

    function clearPaginationQueryParams(): void {
        queryParams.page = null;
    }

    $effect(() => {
        const activeSavedViewId = savedViewsState.activeSavedView?.id;
        if (!activeSavedViewId) {
            return;
        }

        untrack(() => {
            updateFilters(getCurrentFilters(getListFilterQueryParams(page.url.searchParams)), { clearPagination: false });
        });
    });

    function getQueryFilterParams(filters: FacetedFilter.IFilter[]) {
        const botFilter = filters.find((f): f is BooleanFilter => f instanceof BooleanFilter && f.term === 'bot');
        const firstFilter = filters.find((f): f is BooleanFilter => f instanceof BooleanFilter && f.term === 'first');
        const levelFilter = filters.find((f): f is LevelFilter => f.type === 'level');
        const projectFilter = filters.find((f): f is ProjectFilter => f.type === 'project');
        const referenceFilter = filters.find((f): f is ReferenceFilter => f.type === 'reference');
        const sessionFilter = filters.find((f): f is SessionFilter => f.type === 'session');
        const stackFilter = filters.find((f): f is StringFilter => f.type === 'string' && f.key === 'string-stack');
        const statusFilter = filters.find((f): f is StatusFilter => f.type === 'status');
        const tagFilter = filters.find((f): f is TagFilter => f.type === 'tag');
        const typeFilter = filters.find((f): f is TypeFilter => f.type === 'type');
        const versionFilter = filters.find((f): f is VersionFilter => f instanceof VersionFilter && f.term === 'version');

        return {
            bot: botFilter?.value === undefined ? null : String(botFilter.value),
            first: firstFilter?.value === undefined ? null : String(firstFilter.value),
            level: levelFilter?.value.length ? levelFilter.value.join(',') : null,
            project: projectFilter?.value.length ? projectFilter.value.join(',') : null,
            reference: referenceFilter?.value?.trim() ? referenceFilter.value : null,
            session: sessionFilter?.value?.trim() ? sessionFilter.value : null,
            stack: stackFilter?.value?.trim() ? stackFilter.value : null,
            status: statusFilter?.value.length ? statusFilter.value.join(',') : null,
            tag: tagFilter?.value.length ? tagFilter.value.join(',') : null,
            type: typeFilter?.value.length ? typeFilter.value.join(',') : null,
            version: versionFilter?.value?.trim() ? versionFilter.value : null
        };
    }

    function getQueryFilterParamDeltas(currentParams: ReturnType<typeof getQueryFilterParams>, baseParams: ReturnType<typeof getQueryFilterParams>) {
        const getDelta = (currentValue: null | string, baseValue: null | string): null | string => {
            if (currentValue === baseValue) {
                return null;
            }

            return currentValue ?? (baseValue ? '' : null);
        };

        return {
            bot: getDelta(currentParams.bot, baseParams.bot),
            first: getDelta(currentParams.first, baseParams.first),
            level: getDelta(currentParams.level, baseParams.level),
            project: getDelta(currentParams.project, baseParams.project),
            reference: getDelta(currentParams.reference, baseParams.reference),
            session: getDelta(currentParams.session, baseParams.session),
            stack: getDelta(currentParams.stack, baseParams.stack),
            status: getDelta(currentParams.status, baseParams.status),
            tag: getDelta(currentParams.tag, baseParams.tag),
            type: getDelta(currentParams.type, baseParams.type),
            version: getDelta(currentParams.version, baseParams.version)
        };
    }

    function isQueryParamFilter(filter: FacetedFilter.IFilter): boolean {
        if (filter.type === 'string' && filter.key === 'string-stack') {
            return true;
        }

        if (filter.type === 'boolean' && filter instanceof BooleanFilter && (filter.term === 'bot' || filter.term === 'first') && filter.value !== undefined) {
            return true;
        }

        if (filter.type === 'version' && filter instanceof VersionFilter && filter.term !== 'version') {
            return false;
        }

        return ['level', 'project', 'reference', 'session', 'status', 'tag', 'type', 'version'].includes(filter.type);
    }

    function getPageSize(): number {
        return queryParams.limit ?? pageSizePreference.current;
    }

    function setPageSize(value: number): void {
        pageSizePreference.current = value;
        queryParams.limit = null;
    }

    $effect(() => {
        if (queryParams.limit === pageSizePreference.current) {
            queryParams.limit = null;
        }
    });

    const eventsQueryParameters: GetEventsParams = $state({
        get filter() {
            return getEffectiveFilter()!;
        },
        set filter(value) {
            queryParams.filter = value;
        },
        get limit() {
            return getPageSize();
        },
        set limit(value) {
            setPageSize(value);
        },
        mode: 'stack_frequent',
        offset: DEFAULT_OFFSET,
        get page() {
            return queryParams.page ?? undefined;
        },
        set page(value) {
            queryParams.page = value ?? null;
        },
        get time() {
            return getQueryTime() ?? undefined;
        },
        set time(value) {
            const baseTime = savedViewsState.activeSavedView?.time ?? DEFAULT_TIME_RANGE;
            queryParams.time = value === baseTime ? null : value ? serializeTimeQueryParam(value) : ALL_TIME_QUERY_VALUE;
        }
    });

    const client = useFetchClient();
    const clientStatus = useFetchClientStatus(client);
    let clientResponse = $state<FetchClientResponse<EventSummaryModel<SummaryTemplateKeys>[]>>();

    const table = createTable(
        getSharedTableOptions<EventSummaryModel<SummaryTemplateKeys>>({
            columnPersistenceKey: 'stacks-column-visibility',
            get columns() {
                return getColumns<EventSummaryModel<SummaryTemplateKeys>>(eventsQueryParameters.mode, {
                    showType: !hasSingleTypeFilter(eventsQueryParameters.filter)
                });
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
        if (!organization.current || isSavedViewRoutePending) {
            return;
        }

        clientResponse = await client.getJSON<EventSummaryModel<SummaryTemplateKeys>[]>(`organizations/${organization.current}/events`, {
            params: {
                ...eventsQueryParameters,
                include: !eventsQueryParameters.page || eventsQueryParameters.page <= 1 ? 'total' : undefined
            } as Record<string, unknown>
        });

        showBillingDialogOnUpgradeProblem(clientResponse.problem, organization.current, () => loadData());
    }

    const throttledLoadData = throttle(5000, loadData);

    async function onStackChanged(message: WebSocketMessageValue<'StackChanged'>) {
        if (message.id && message.change_type === ChangeType.Removed) {
            if (message.id === selectedStackId) {
                selectedStackId = undefined;
            }

            removeTableSelection(table, message.id);

            if (removeTableData(table, (doc: EventSummaryModel<SummaryTemplateKeys>) => doc.id === message.id)) {
                // If the grid data is empty from all events being removed, we should refresh the data.
                if (isTableEmpty(table)) {
                    await throttledLoadData();
                    return;
                }
            }
        }
    }

    useEventListener(document, 'StackChanged', async (event) => await onStackChanged((event as CustomEvent).detail));

    $effect(() => {
        loadData();
    });

    const chartDataQuery = getOrganizationCountQuery({
        enabled: () => !isSavedViewRoutePending,
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
        const timeRange = parseDateMathRange(getQueryTime());

        const buildZeroFilledSeries = () => {
            const series = fillDateSeries(timeRange.start, timeRange.end, (date: Date) => ({
                date,
                events: 0,
                stacks: 0
            }));
            return series;
        };

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
        const timeRange = parseDateMathRange(getQueryTime());
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

    function onRangeSelect(start: Date, end: Date) {
        onFilterChanged(new DateFilter('date', toDateMathRange(start, end)));
    }
</script>

{#if savedViewsState.isMissing}
    {throwSavedViewNotFound()}
{/if}

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
                    onResetToSaved={handleResetToSaved}
                    savedViews={savedViewsState.savedViews}
                    {showChart}
                    {showStats}
                    setShowChart={(v) => (showChart = v)}
                    setShowStats={(v) => (showStats = v)}
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
                isLoading={isSavedViewRoutePending || (chartDataQuery.isLoading && !chartDataQuery.isSuccess)}
                newStacks={stats.newStacks}
                totalEvents={stats.totalEvents}
                totalStacks={stats.totalStacks}
            />
        {/if}

        {#if showChart}
            <EventsDashboardChart
                data={chartData()}
                isLoading={isSavedViewRoutePending || clientStatus.isLoading || chartDataQuery.isLoading}
                {onRangeSelect}
            />
        {/if}

        <EventsDataTable bind:limit={eventsQueryParameters.limit!} isLoading={isSavedViewRoutePending || clientStatus.isLoading} {rowClick} {rowHref} {table}>
            {#snippet footerChildren()}
                <div class="h-9 min-w-35">
                    <TableStacksBulkActionsDropdownMenu {table} />
                </div>

                <DataTable.Selection {table} />
                <DataTable.PageSize bind:value={eventsQueryParameters.limit!} {table}></DataTable.PageSize>
                <div class="flex items-center space-x-6 lg:space-x-8">
                    <DataTable.PageCount {table} />
                    <DataTable.Pagination {table} />
                </div>
            {/snippet}
        </EventsDataTable>
    </div>
</div>

<StackDetailSheet
    stackId={selectedStackId}
    filterChanged={onFilterChanged}
    onClose={() => {
        selectedStackId = undefined;
    }}
    onError={handleStackError}
/>
