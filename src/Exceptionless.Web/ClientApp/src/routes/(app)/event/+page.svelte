<script lang="ts">
    import type { GetEventsParams } from '$features/events/api.svelte';
    import type { EventSummaryModel, SummaryTemplateKeys } from '$features/events/components/summary/index';
    import type { PersistentEvent } from '$features/events/models';

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
    import { createPageSizePreference, getSharedTableOptions, isTableEmpty, removeTableData, removeTableSelection } from '$features/shared/table.svelte';
    import { fillDateSeries } from '$features/shared/utils/charts.js';
    import { toDateMathRange } from '$features/shared/utils/datemath';
    import { parseDateMathRange } from '$features/shared/utils/datemath.js';
    import { StackStatus } from '$features/stacks/models';
    import { ChangeType, type WebSocketMessageValue } from '$features/websockets/models';
    import { DEFAULT_OFFSET, useFetchClientStatus } from '$shared/api/api.svelte';
    import { type FetchClientResponse, type ProblemDetails, useFetchClient } from '@exceptionless/fetchclient';
    import { error } from '@sveltejs/kit';
    import { createTable } from '@tanstack/svelte-table';
    import { queryParamsState } from 'kit-query-params';
    import { useEventListener, watch } from 'runed';
    import { untrack } from 'svelte';
    import { debounce, throttle } from 'throttle-debounce';

    import {
        ALL_TIME_QUERY_VALUE,
        deserializeTimeQueryParam,
        getEventsNavigationOptionsForFilter,
        redirectToEventsWithFilter,
        serializeTimeQueryParam
    } from '../redirect-to-events.svelte';

    let selectedEventId: null | string = $state(null);
    let selectedEvent = $state<null | PersistentEvent>(null);

    function handleEventError(problem: ProblemDetails) {
        showBillingDialogOnUpgradeProblem(problem, organization.current);
        selectedEventId = null;
        selectedEvent = null;
    }

    function rowClick(row: EventSummaryModel<SummaryTemplateKeys>) {
        selectedEventId = row.id;
        selectedEvent = toInitialEvent(row);
    }

    function rowHref(row: EventSummaryModel<SummaryTemplateKeys>): string {
        return resolve('/(app)/event/[eventId=objectid]', { eventId: row.id });
    }

    function toInitialEvent(row: EventSummaryModel<SummaryTemplateKeys>): PersistentEvent {
        const rowData = row.data as Record<string, unknown>;
        const rowWithIds = row as EventSummaryModel<SummaryTemplateKeys> & {
            organization_id?: string;
            project_id?: string;
            stack_id?: string;
        };
        const type = getString(rowData.Type) ?? (row.template_key === 'event-notfound-summary' ? '404' : undefined);
        const source = getString(rowData.Source) ?? getString(rowData.Path);
        const message = getString(rowData.Message) ?? getString(rowData.Name) ?? source ?? type ?? '';
        const identity = getString(rowData.Identity);

        return {
            created_utc: row.date,
            data: {
                ...(identity ? { '@user': { identity } } : {})
            },
            date: row.date,
            id: row.id,
            is_first_occurrence: false,
            message,
            organization_id: rowWithIds.organization_id ?? organization.current ?? '',
            project_id: rowWithIds.project_id ?? '',
            source,
            stack_id: rowWithIds.stack_id ?? queryParams.stack ?? '',
            type
        };
    }

    function getString(value: unknown): string | undefined {
        return typeof value === 'string' && value.length > 0 ? value : undefined;
    }

    const DEFAULT_TIME_RANGE = '[now-7d TO now]';
    const DEFAULT_FILTER = '(status:open OR status:regressed)';
    const DEFAULT_FILTERS = [new DateFilter('date', DEFAULT_TIME_RANGE), new ProjectFilter([]), new StatusFilter([StackStatus.Open, StackStatus.Regressed])];
    const PAGE_SIZE_PREFERENCE_KEY = 'event-stack-list-page-size';
    const pageSizePreference = createPageSizePreference(PAGE_SIZE_PREFERENCE_KEY);
    const DEFAULT_PARAMS = {
        after: undefined as string | undefined,
        before: undefined as string | undefined,
        bot: undefined as string | undefined,
        filter: undefined as string | undefined,
        first: undefined as string | undefined,
        level: undefined as string | undefined,
        limit: undefined as number | undefined,
        page: undefined as number | undefined,
        project: undefined as string | undefined,
        reference: undefined as string | undefined,
        session: undefined as string | undefined,
        sort: undefined as string | undefined,
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

    function getQueryTime(): null | string {
        if (queryParams.time != null) {
            if (queryParams.time === ALL_TIME_QUERY_VALUE) {
                return null;
            }

            return queryParams.time ? deserializeTimeQueryParam(queryParams.time) : null;
        }

        return savedViewsState.activeSavedView?.time ?? DEFAULT_TIME_RANGE;
    }

    function getEffectiveFilter(): null | string {
        const filter = toFilter(getCurrentFiltersWithoutTime());
        return filter || null;
    }

    function getQueryFilters(): FacetedFilter.IFilter[] | null {
        const filters: FacetedFilter.IFilter[] = [];

        if (queryParams.project) {
            filters.push(new ProjectFilter(splitQueryParam(queryParams.project)));
        }

        if (queryParams.stack) {
            filters.push(new StringFilter('stack', queryParams.stack));
        }

        const bot = parseBooleanQueryParam(queryParams.bot);
        if (bot !== undefined) {
            filters.push(new BooleanFilter('bot', bot));
        }

        const first = parseBooleanQueryParam(queryParams.first);
        if (first !== undefined) {
            filters.push(new BooleanFilter('first', first));
        }

        if (queryParams.level) {
            filters.push(new LevelFilter(splitQueryParam(queryParams.level) as never[]));
        }

        if (queryParams.reference) {
            filters.push(new ReferenceFilter(queryParams.reference));
        }

        if (queryParams.session) {
            filters.push(new SessionFilter(queryParams.session));
        }

        if (queryParams.status) {
            filters.push(new StatusFilter(splitQueryParam(queryParams.status) as never[]));
        }

        if (queryParams.tag) {
            filters.push(new TagFilter(splitQueryParam(queryParams.tag) as never[]));
        }

        if (queryParams.type) {
            filters.push(new TypeFilter(splitQueryParam(queryParams.type) as never[]));
        }

        if (queryParams.version) {
            filters.push(new VersionFilter('version', queryParams.version));
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
            after: 'string',
            before: 'string',
            bot: 'string',
            filter: 'string',
            first: 'string',
            level: 'string',
            limit: 'number',
            page: 'number',
            project: 'string',
            reference: 'string',
            session: 'string',
            sort: 'string',
            stack: 'string',
            status: 'string',
            tag: 'string',
            time: 'string',
            type: 'string',
            version: 'string'
        }
    });

    const VIEW = 'events';
    let showStats = $state(true);
    let showChart = $state(true);
    const savedViewsState = useSavedViews({
        baseHref: resolve('/(app)/event'),
        defaultColumnVisibility: defaultEventColumnVisibility,
        defaultFilter: DEFAULT_FILTER,
        defaultTime: DEFAULT_TIME_RANGE,
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

    function throwSavedViewNotFound(): never {
        throw error(404, `The saved Events view "${page.params.slug}" could not be found.`);
    }

    // NOTE: This might be applying query string parameters when redirecting away.
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

    function getCurrentFilters(): FacetedFilter.IFilter[] {
        return applyTimeFilter(getCurrentFiltersWithoutTime(), getQueryTime());
    }

    function getCurrentFiltersWithoutTime(): FacetedFilter.IFilter[] {
        const savedViewFilters = getSavedViewFilters();
        const queryFilters = getQueryFilters() ?? [];
        const expressionFilters =
            queryParams.filter != null
                ? getFiltersFromCache(filterCacheKey(queryParams.filter), queryParams.filter).filter((filter) => filter.type !== 'date')
                : [];

        if (savedViewFilters) {
            return mergeFilterOverrides(
                savedViewFilters.filter((filter) => filter.type !== 'date'),
                [...expressionFilters, ...queryFilters],
                getQueryFilterRemovalKeys(savedViewFilters)
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

    function getQueryFilterRemovalKeys(savedViewFilters: FacetedFilter.IFilter[]): string[] {
        const removedKeys: string[] = [];

        if (queryParams.bot === '') {
            removedKeys.push('boolean-bot');
        }

        if (queryParams.first === '') {
            removedKeys.push('boolean-first');
        }

        if (queryParams.filter === '') {
            removedKeys.push(...savedViewFilters.filter((filter) => filter.type !== 'date' && !isQueryParamFilter(filter)).map((filter) => filter.key));
        }

        if (queryParams.level === '') {
            removedKeys.push('level');
        }

        if (queryParams.project === '') {
            removedKeys.push('project');
        }

        if (queryParams.reference === '') {
            removedKeys.push('reference');
        }

        if (queryParams.session === '') {
            removedKeys.push('session');
        }

        if (queryParams.stack === '') {
            removedKeys.push('string-stack');
        }

        if (queryParams.status === '') {
            removedKeys.push('status');
        }

        if (queryParams.tag === '') {
            removedKeys.push('tag');
        }

        if (queryParams.type === '') {
            removedKeys.push('type');
        }

        if (queryParams.version === '') {
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
        () => {
            if (isInternalFilterUpdate) {
                isInternalFilterUpdate = false;
                return;
            }

            filters = getCurrentFilters();
        },
        { lazy: true }
    );

    async function onFilterChanged(addedOrUpdated: FacetedFilter.IFilter): Promise<void> {
        const navigationOptions = getEventsNavigationOptionsForFilter(addedOrUpdated);
        if (navigationOptions) {
            selectedEventId = null;
            selectedEvent = null;
            await redirectToEventsWithFilter(organization.current, addedOrUpdated, navigationOptions);
            return;
        }

        const isNew = !filters?.some((f) => f.id === addedOrUpdated.id);
        const updatedFilters = filterChanged(filters ?? [], addedOrUpdated);
        updateFilters(updatedFilters);
        if (isNew) {
            filters = updatedFilters;
        }

        selectedEventId = null;
        selectedEvent = null;
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

        updateFilterCache(filterCacheKey(filter), updatedFilters);
        if (shouldClearPagination) {
            clearPaginationQueryParams();
        }

        // Only skip the watch when the URL will actually change from our update.
        // If the URL doesn't change, the watch won't fire and the flag would stay stale.
        if (
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
            queryFilterParams.version !== queryParams.version
        ) {
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
        queryParams.after = null;
        queryParams.before = null;
        queryParams.page = null;
    }

    $effect(() => {
        const activeSavedViewId = savedViewsState.activeSavedView?.id;
        if (!activeSavedViewId) {
            return;
        }

        untrack(() => {
            updateFilters(getCurrentFilters(), { clearPagination: false });
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
        get after() {
            return queryParams.after ?? undefined;
        },
        set after(value) {
            queryParams.after = value ?? null;
        },
        get before() {
            return queryParams.before ?? undefined;
        },
        set before(value) {
            queryParams.before = value ?? null;
        },
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
        mode: 'summary',
        offset: DEFAULT_OFFSET,
        get page() {
            return queryParams.page ?? undefined;
        },
        set page(value) {
            queryParams.page = value ?? null;
        },
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
            queryParams.time = value ? serializeTimeQueryParam(value) : ALL_TIME_QUERY_VALUE;
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

        const params = { ...eventsQueryParameters };
        delete params.page;
        clientResponse = await client.getJSON<EventSummaryModel<SummaryTemplateKeys>[]>(`organizations/${organization.current}/events`, { params });

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

        <EventsDataTable bind:limit={eventsQueryParameters.limit!} isLoading={clientStatus.isLoading} {rowClick} {rowHref} {table}>
            {#snippet footerChildren()}
                <div class="h-9 min-w-35">
                    {#if table.getSelectedRowModel().flatRows.length}
                        <EventsBulkActionsDropdownMenu {table} />
                    {/if}
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

<EventDetailSheet
    eventId={selectedEventId}
    filterChanged={onFilterChanged}
    initialEvent={selectedEvent}
    onClose={() => {
        selectedEventId = null;
        selectedEvent = null;
    }}
    onError={handleEventError}
/>
