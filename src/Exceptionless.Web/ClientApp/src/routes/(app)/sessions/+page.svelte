<script lang="ts">
    import type { GetEventsParams } from '$features/events/api.svelte';

    import { resolve } from '$app/paths';
    import { page } from '$app/state';
    import * as DataTable from '$comp/data-table';
    import * as FacetedFilter from '$comp/faceted-filter';
    import RefreshButton from '$comp/refresh-button.svelte';
    import { H3 } from '$comp/typography';
    import { Label } from '$comp/ui/label';
    import { Switch } from '$comp/ui/switch';
    import { showBillingDialogOnUpgradeProblem } from '$features/billing/upgrade-required.svelte';
    import { getOrganizationSessionsCountQuery, getOrganizationSessionsQuery, PERSISTENT_EVENT_DELETE_RECONCILE_EVENT } from '$features/events/api.svelte';
    import EventDetailSheet from '$features/events/components/event-detail-sheet.svelte';
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
        serializeFilters,
        toFilter,
        updateFilterCache
    } from '$features/events/components/filters/helpers.svelte';
    import OrganizationDefaultsFacetedFilterBuilder from '$features/events/components/filters/organization-defaults-faceted-filter-builder.svelte';
    import { buildEventDetailsHref, type EventSummaryModel, type SummaryTemplateKeys } from '$features/events/components/summary/index';
    import EventsDataTable from '$features/events/components/table/events-data-table.svelte';
    import { getOrganizationQuery } from '$features/organizations/api.svelte';
    import { organization } from '$features/organizations/context.svelte';
    import { premiumPage } from '$features/organizations/premium-page.svelte';
    import SavedViewPicker from '$features/saved-views/components/saved-view-picker.svelte';
    import { isSavedViewHydrationPending, isSavedViewUnavailable, useSavedViews } from '$features/saved-views/use-saved-views.svelte';
    import { defaultSessionColumnVisibility, getSessionColumns } from '$features/sessions/components/session-table-columns';
    import SessionsDashboardChart from '$features/sessions/components/sessions-dashboard-chart.svelte';
    import SessionsStatsDashboard from '$features/sessions/components/sessions-stats-dashboard.svelte';
    import * as agg from '$features/shared/api/aggregations';
    import { createPageSizePreference, getSharedTableOptions, removeTableData, removeTableSelection } from '$features/shared/table.svelte';
    import { fillDateSeries } from '$features/shared/utils/charts.js';
    import { parseDateMathRange, toDateMathRange } from '$features/shared/utils/datemath';
    import { ChangeType, type WebSocketMessageValue } from '$features/websockets/models';
    import { DEFAULT_OFFSET } from '$shared/api/api.svelte';
    import { createQueryParameters } from '$shared/query-params';
    import { error } from '@sveltejs/kit';
    import { createTable } from '@tanstack/svelte-table';
    import { useEventListener, watch } from 'runed';
    import { onDestroy, untrack } from 'svelte';
    import { debounce } from 'throttle-debounce';

    import {
        ALL_TIME_QUERY_VALUE,
        deserializeTimeQueryParam,
        getListFilterQueryParams,
        LIST_FILTER_QUERY_PARAM_RESET,
        type ListFilterQueryParams,
        serializeTimeQueryParam
    } from '../redirect-to-events.svelte';

    type SessionListFilterQueryParams = ListFilterQueryParams & {
        filters?: null | string;
    };

    const ACTIVE_SESSION_END_TERM = 'data.sessionend';
    const DEFAULT_FILTER = 'type:session';
    const DEFAULT_TIME_RANGE = '[now-7d TO now]';
    const DEFAULT_FILTERS = [new DateFilter('date', DEFAULT_TIME_RANGE), new ProjectFilter([]), new TypeFilter(['session'])];
    const PAGE_SIZE_PREFERENCE_KEY = 'event-stack-list-page-size';
    const pageSizePreference = createPageSizePreference(PAGE_SIZE_PREFERENCE_KEY);
    const DEFAULT_PARAMS = {
        after: undefined as string | undefined,
        before: undefined as string | undefined,
        bot: undefined as string | undefined,
        filter: undefined as string | undefined,
        filters: undefined as string | undefined,
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

    let selectedEventId: null | string = $state(null);

    function rowClick(row: EventSummaryModel<SummaryTemplateKeys>) {
        selectedEventId = row.id;
    }

    function rowHref(row: EventSummaryModel<SummaryTemplateKeys>): string {
        return buildEventDetailsHref(row.id);
    }

    premiumPage.current = 'Sessions';

    const organizationQuery = getOrganizationQuery({
        route: {
            get id() {
                return organization.current;
            }
        }
    });
    const hasPremiumFeatures = $derived(organizationQuery.isSuccess && !!organizationQuery.data?.has_premium_features);

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
        const queryFilters: FacetedFilter.IFilter[] = [];

        if (params.project) {
            queryFilters.push(new ProjectFilter(splitQueryParam(params.project)));
        }

        if (params.stack) {
            queryFilters.push(new StringFilter('stack', params.stack));
        }

        const bot = parseBooleanQueryParam(params.bot);
        if (bot !== undefined) {
            queryFilters.push(new BooleanFilter('bot', bot));
        }

        const first = parseBooleanQueryParam(params.first);
        if (first !== undefined) {
            queryFilters.push(new BooleanFilter('first', first));
        }

        if (params.level) {
            queryFilters.push(new LevelFilter(splitQueryParam(params.level) as never[]));
        }

        if (params.reference) {
            queryFilters.push(new ReferenceFilter(params.reference));
        }

        if (params.session) {
            queryFilters.push(new SessionFilter(params.session));
        }

        if (params.status) {
            queryFilters.push(new StatusFilter(splitQueryParam(params.status) as never[]));
        }

        if (params.tag) {
            queryFilters.push(new TagFilter(splitQueryParam(params.tag) as never[]));
        }

        if (params.type) {
            queryFilters.push(new TypeFilter(splitQueryParam(params.type) as never[]));
        }

        if (params.version) {
            queryFilters.push(new VersionFilter('version', params.version));
        }

        return queryFilters.length > 0 ? queryFilters : null;
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
    const queryParams = createQueryParameters({
        defaults: DEFAULT_PARAMS,
        history: 'push',
        schema: {
            after: 'string',
            before: 'string',
            bot: 'string',
            filter: 'string',
            filters: 'string',
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

    const VIEW = 'sessions';
    let showStats = $state(true);
    let showChart = $state(true);
    const savedViewsState = useSavedViews({
        applyFilters: (draftFilters, options) => {
            updateFilters(draftFilters, {
                clearPagination: false,
                history: options?.history
            });
            filters = draftFilters;
        },
        baseHref: resolve('/(app)/sessions'),
        defaultAutoFillColumnId: 'summary',
        defaultColumnVisibility: defaultSessionColumnVisibility,
        defaultFilter: DEFAULT_FILTER,
        defaultTime: DEFAULT_TIME_RANGE,
        filterCacheKey,
        getAvailableColumnIds: () =>
            table
                .getAllFlatColumns()
                .filter((column) => column.columns.length === 0)
                .map((column) => column.id),
        getColumnOrder: () => table.store.state.columnOrder,
        getColumnSizing: () => table.store.state.columnSizing,
        getColumnVisibility: () => table.store.state.columnVisibility,
        getFilter: getEffectiveFilter,
        getFilterDefinitions: () => serializeFilters(filters ?? []),
        getShowChart: () => showChart,
        getShowStats: () => showStats,
        getSort: getEffectiveSort,
        getTime: getQueryTime,
        queryParams,
        setColumnOrder: (value) => table.setColumnOrder(value),
        setColumnSizing: (value) => table.setColumnSizing(value),
        setColumnVisibility: (value) => table.setColumnVisibility(value),
        setShowChart: (value) => (showChart = value),
        setShowStats: (value) => (showStats = value),
        get slug() {
            return page.params.slug;
        },
        updateFilterCache,
        view: VIEW
    });
    const pageTitle = $derived(savedViewsState.activeSavedView?.name ?? 'Sessions');
    let normalizedSavedViewId = $state<string>();
    const isSavedViewRoutePending = $derived(
        isSavedViewHydrationPending(
            page.params.slug,
            savedViewsState.activeSavedView?.id,
            normalizedSavedViewId,
            isSavedViewUnavailable(savedViewsState.activeSavedView?.id, savedViewsState.isMissing, savedViewsState.isError)
        )
    );

    $effect(() => {
        document.title = `${pageTitle} - Exceptionless`;
    });

    function throwSavedViewNotFound(): never {
        throw error(404, `The saved Sessions view "${page.params.slug}" could not be found.`);
    }

    watch(
        () => organization.current,
        (_currentOrganizationId, previousOrganizationId) => {
            if (previousOrganizationId === undefined) {
                return;
            }
            updateFilterCache(filterCacheKey(DEFAULT_FILTER), DEFAULT_FILTERS);
            queryParams.update(DEFAULT_PARAMS);
            reset();
        },
        {
            lazy: true
        }
    );

    function getSessionListFilterQueryParams(params: typeof queryParams = queryParams): SessionListFilterQueryParams {
        return {
            ...getListFilterQueryParams(params),
            filters: params.filters
        };
    }

    function getCurrentFilters(params: SessionListFilterQueryParams = getSessionListFilterQueryParams()): FacetedFilter.IFilter[] {
        return applyTimeFilter(getCurrentFiltersWithoutTime(params), getQueryTime(params));
    }

    function getCurrentFiltersWithoutTime(params: SessionListFilterQueryParams = getSessionListFilterQueryParams()): FacetedFilter.IFilter[] {
        const savedViewFilters = getSavedViewFilters();
        const queryFilters = getQueryFilters(params) ?? [];
        const serializedExpressionFilters = params.filters != null && params.filters ? deserializeFilters(params.filters) : [];
        const rawExpressionFilters =
            params.filter != null && params.filter
                ? getFiltersFromCache(filterCacheKey(params.filter), params.filter).filter((filter) => filter.type !== 'date')
                : [];
        const expressionFilters = [...rawExpressionFilters, ...serializedExpressionFilters];

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
        return getFiltersFromCache(filterCacheKey(filter), filter).filter((currentFilter) => currentFilter.type !== 'date');
    }

    function getSavedViewFilters(): FacetedFilter.IFilter[] | null {
        const savedView = savedViewsState.activeSavedView;
        return savedView?.filter_definitions ? deserializeFilters(savedView.filter_definitions) : null;
    }

    function getQueryFilterRemovalKeys(savedViewFilters: FacetedFilter.IFilter[], params: SessionListFilterQueryParams): string[] {
        const removedKeys: string[] = [];

        if (params.bot === '') {
            removedKeys.push('boolean-bot');
        }

        if (params.first === '') {
            removedKeys.push('boolean-first');
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

        if (params.filter === '' || params.filters === '') {
            removedKeys.push(...savedViewFilters.filter((filter) => filter.type !== 'date' && !isQueryParamFilter(filter)).map((filter) => filter.key));
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
        [() => page.url.pathname, () => getSessionListFilterQueryParams(), () => savedViewsState.activeSavedView],
        ([pathname, currentQueryParams, activeSavedView], [previousPathname, previousQueryParams, previousSavedView]) => {
            const savedViewChanged = pathname !== previousPathname || activeSavedView?.id !== previousSavedView?.id;
            const queryChanged = JSON.stringify(currentQueryParams) !== JSON.stringify(previousQueryParams);
            if (savedViewChanged || queryChanged) {
                table.resetRowSelection();
            }

            if (isInternalFilterUpdate && !savedViewChanged) {
                isInternalFilterUpdate = false;
                return;
            }

            isInternalFilterUpdate = false;
            filters = getCurrentFilters(currentQueryParams);
        },
        {
            lazy: true
        }
    );

    function handleResetToSaved(): void {
        isInternalFilterUpdate = false;
        table.resetRowSelection();
        queryParams.update({
            ...LIST_FILTER_QUERY_PARAM_RESET,
            filters: null
        });
        savedViewsState.handleResetToSaved();
        filters = getCurrentFilters();
    }

    function onFilterChanged(addedOrUpdated: FacetedFilter.IFilter): void {
        const isNew = !filters?.some((filter) => filter.id === addedOrUpdated.id);
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

    function updateFilters(updatedFilters: FacetedFilter.IFilter[], options: { clearPagination?: boolean; history?: 'push' | 'replace' } = {}): void {
        const shouldClearPagination = options.clearPagination ?? true;
        const filter = toFilter(updatedFilters.filter((currentFilter) => currentFilter.type !== 'date'));
        const expressionFilters = updatedFilters.filter((currentFilter) => currentFilter.type !== 'date' && !isQueryParamFilter(currentFilter));
        const time = ((updatedFilters.find((currentFilter) => currentFilter.type === 'date') as DateFilter | undefined)?.value as string | undefined) ?? null;
        const baseTime = savedViewsState.activeSavedView?.time ?? DEFAULT_TIME_RANGE;
        const savedViewFilters = getSavedViewFilters();
        const baseQueryFilterParams = getQueryFilterParams(savedViewFilters ?? []);
        const queryFilterParams = getQueryFilterParamDeltas(getQueryFilterParams(updatedFilters), baseQueryFilterParams);
        const baseExpressionFilters = savedViewFilters?.filter((currentFilter) => currentFilter.type !== 'date' && !isQueryParamFilter(currentFilter)) ?? [];
        const serializedExpressionFilters = serializeFilters(expressionFilters);
        const serializedBaseExpressionFilters = serializeFilters(baseExpressionFilters);

        const newFiltersParam =
            serializedExpressionFilters === serializedBaseExpressionFilters
                ? null
                : expressionFilters.length > 0
                  ? serializedExpressionFilters
                  : baseExpressionFilters.length > 0
                    ? ''
                    : null;
        const newTimeParam = time === baseTime ? null : time ? serializeTimeQueryParam(time) : ALL_TIME_QUERY_VALUE;
        const urlQueryWillChange =
            queryParams.filter != null ||
            newFiltersParam !== queryParams.filters ||
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
        const paginationWillChange = shouldClearPaginationForFilter && (queryParams.after != null || queryParams.before != null || queryParams.page != null);

        if (effectiveQueryWillChange) {
            table.resetRowSelection();
        }
        updateFilterCache(filterCacheKey(filter), updatedFilters);
        if (paginationWillChange || urlQueryWillChange) {
            isInternalFilterUpdate = true;
        }

        queryParams.update(
            {
                after: shouldClearPaginationForFilter ? null : queryParams.after,
                before: shouldClearPaginationForFilter ? null : queryParams.before,
                bot: queryFilterParams.bot,
                filter: null,
                filters: newFiltersParam,
                first: queryFilterParams.first,
                level: queryFilterParams.level,
                page: shouldClearPaginationForFilter ? null : queryParams.page,
                project: queryFilterParams.project,
                reference: queryFilterParams.reference,
                session: queryFilterParams.session,
                stack: queryFilterParams.stack,
                status: queryFilterParams.status,
                tag: queryFilterParams.tag,
                time: newTimeParam,
                type: queryFilterParams.type,
                version: queryFilterParams.version
            },
            {
                history: options.history
            }
        );
    }

    $effect(() => {
        const activeSavedViewId = savedViewsState.activeSavedView?.id;
        if (!activeSavedViewId || activeSavedViewId !== savedViewsState.hydratedSavedViewId) {
            normalizedSavedViewId = undefined;
            return;
        }

        untrack(() => {
            updateFilters(getCurrentFilters(getSessionListFilterQueryParams()), {
                clearPagination: false,
                history: 'replace'
            });
        });
        normalizedSavedViewId = activeSavedViewId;
    });

    function getQueryFilterParams(currentFilters: FacetedFilter.IFilter[]) {
        const botFilter = currentFilters.find((filter): filter is BooleanFilter => filter instanceof BooleanFilter && filter.term === 'bot');
        const firstFilter = currentFilters.find((filter): filter is BooleanFilter => filter instanceof BooleanFilter && filter.term === 'first');
        const levelFilter = currentFilters.find((filter): filter is LevelFilter => filter.type === 'level');
        const projectFilter = currentFilters.find((filter): filter is ProjectFilter => filter.type === 'project');
        const referenceFilter = currentFilters.find((filter): filter is ReferenceFilter => filter.type === 'reference');
        const sessionFilter = currentFilters.find((filter): filter is SessionFilter => filter.type === 'session');
        const stackFilter = currentFilters.find((filter): filter is StringFilter => filter.type === 'string' && filter.key === 'string-stack');
        const statusFilter = currentFilters.find((filter): filter is StatusFilter => filter.type === 'status');
        const tagFilter = currentFilters.find((filter): filter is TagFilter => filter.type === 'tag');
        const typeFilter = currentFilters.find((filter): filter is TypeFilter => filter.type === 'type');
        const versionFilter = currentFilters.find((filter): filter is VersionFilter => filter instanceof VersionFilter && filter.term === 'version');

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

    const viewActive = $derived(
        filters.some((filter) => filter instanceof BooleanFilter && filter.term === ACTIVE_SESSION_END_TERM && filter.value === undefined)
    );

    function setViewActive(value: boolean): void {
        const activeFilter = filters.find(
            (filter): filter is BooleanFilter => filter instanceof BooleanFilter && filter.term === ACTIVE_SESSION_END_TERM && filter.value === undefined
        );
        if (value === !!activeFilter) {
            return;
        }

        const updatedFilters = activeFilter ? filterRemoved(filters, activeFilter) : [...filters];
        if (!activeFilter) {
            const filter = new BooleanFilter(ACTIVE_SESSION_END_TERM);
            filter.hidden = true;
            updatedFilters.push(filter);
        }

        updateFilters(updatedFilters);
        filters = updatedFilters;
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
            return getEffectiveFilter() ?? undefined;
        },
        set filter(value) {
            queryParams.filter = value ?? null;
        },
        get limit() {
            return getPageSize();
        },
        set limit(value) {
            setPageSize(value ?? pageSizePreference.current);
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

    const sessionsQuery = getOrganizationSessionsQuery({
        enabled: () => hasPremiumFeatures && !isSavedViewRoutePending,
        get params() {
            const { page: ignoredPage, ...params } = {
                ...eventsQueryParameters,
                include: !eventsQueryParameters.after && !eventsQueryParameters.before ? ('total' as const) : undefined
            };
            void ignoredPage;
            return params;
        },
        route: {
            get organizationId() {
                return organization.current;
            }
        }
    });

    const table = createTable(
        getSharedTableOptions<EventSummaryModel<SummaryTemplateKeys>>({
            columnPersistenceKey: 'sessions-column-visibility',
            get columns() {
                return getSessionColumns();
            },
            defaultColumnVisibility: defaultSessionColumnVisibility,
            enableColumnResizing: true,
            paginationStrategy: 'cursor',
            get queryData() {
                return sessionsQuery.data?.data ?? [];
            },
            get queryMeta() {
                return sessionsQuery.data?.meta;
            },
            get queryParameters() {
                return eventsQueryParameters;
            }
        })
    );

    function reset() {
        table.resetRowSelection();
        table.setPageIndex(0);
    }

    async function handleRefresh() {
        table.resetRowSelection();
        await Promise.all([sessionsQuery.refetch(), statsQuery.refetch()]);
    }

    const debouncedRefetch = debounce(1500, () => {
        void sessionsQuery.refetch();
        void statsQuery.refetch();
    });
    onDestroy(() => debouncedRefetch.cancel());

    function onPersistentEventChanged(message: WebSocketMessageValue<'PersistentEventChanged'>) {
        if (message.id && message.change_type === ChangeType.Removed) {
            removeTableSelection(table, message.id);
            removeTableData(table, (document) => document.id === message.id);
        }
    }

    useEventListener(document, PERSISTENT_EVENT_DELETE_RECONCILE_EVENT, () => debouncedRefetch());
    useEventListener(document, 'refresh', handleRefresh);
    useEventListener(document, 'PersistentEventChanged', (event) => onPersistentEventChanged((event as CustomEvent).detail));

    let lastEmptyResponseAt = 0;
    $effect(() => {
        const dataUpdatedAt = sessionsQuery.dataUpdatedAt;
        if (
            sessionsQuery.isPlaceholderData ||
            dataUpdatedAt === lastEmptyResponseAt ||
            sessionsQuery.data?.data?.length !== 0 ||
            table.store.state.pagination.pageIndex === 0
        ) {
            return;
        }

        lastEmptyResponseAt = dataUpdatedAt;
        untrack(() => table.previousPage());
    });

    let lastProblem: unknown;
    $effect(() => {
        const problem = sessionsQuery.error ?? sessionsQuery.data?.problem;
        if (!problem || problem === lastProblem) {
            return;
        }
        lastProblem = problem;
        untrack(() =>
            showBillingDialogOnUpgradeProblem(problem, organization.current, async () => {
                await sessionsQuery.refetch();
            })
        );
    });

    const statsQuery = getOrganizationSessionsCountQuery({
        enabled: () => hasPremiumFeatures && !isSavedViewRoutePending,
        params: {
            get aggregations() {
                return `avg:value cardinality:user date:(date${DEFAULT_OFFSET ? `^${DEFAULT_OFFSET}` : ''} cardinality:user)`;
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

    const stats = $derived.by(() => {
        const aggregations = statsQuery.data?.aggregations;
        const total = statsQuery.data?.total ?? 0;
        const timeRange = parseDateMathRange(getQueryTime() || undefined);
        const hours = Math.max((timeRange.end.getTime() - timeRange.start.getTime()) / 3_600_000, 1);
        return {
            avgDuration: agg.average(aggregations, 'avg_value')?.value ?? 0,
            avgPerHour: total / hours,
            totalSessions: total,
            totalUsers: agg.cardinality(aggregations, 'cardinality_user')?.value ?? 0
        };
    });

    const chartData = $derived.by(() => {
        const timeRange = parseDateMathRange(getQueryTime() || undefined);
        const buildZeroFilledSeries = () =>
            fillDateSeries(timeRange.start, timeRange.end, (date: Date) => ({
                date,
                sessions: 0,
                users: 0
            }));
        const dateHistogramBuckets = agg.dateHistogram(statsQuery.data?.aggregations, 'date_date')?.buckets ?? [];
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
</script>

{#if savedViewsState.isMissing}
    {throwSavedViewNotFound()}
{/if}

<div class="flex flex-col">
    <div class="mb-4 flex flex-wrap items-start gap-2">
        <H3 class="my-0 shrink-0">{pageTitle}</H3>
        <div class="order-3 flex w-full flex-wrap items-start gap-1.5 md:order-none md:w-auto md:min-w-0 md:flex-1">
            <FacetedFilter.Root changed={onFilterChanged} {filters} remove={onFilterRemoved}>
                <OrganizationDefaultsFacetedFilterBuilder />
            </FacetedFilter.Root>
        </div>
        <div class="ml-auto flex shrink-0 items-start gap-2">
            <div class="flex h-9 items-center gap-2">
                <Switch checked={viewActive} disabled={!hasPremiumFeatures} id="view-active" onCheckedChange={setViewActive} />
                <Label class="text-sm" for="view-active">View Active</Label>
            </div>
            {#if savedViewsState.isEnabled}
                <SavedViewPicker
                    activeSavedView={savedViewsState.activeSavedView}
                    autoFillColumnId={savedViewsState.autoFillColumnId}
                    canModifySavedView={savedViewsState.canModifySavedView}
                    columnOrder={table.store.state.columnOrder}
                    columnSizing={table.store.state.columnSizing}
                    columnVisibility={table.store.state.columnVisibility}
                    defaultAutoFillColumnId="summary"
                    filters={filters ?? []}
                    isModified={savedViewsState.isModified}
                    onLoadView={savedViewsState.handleLoadView}
                    onClearSavedView={savedViewsState.handleClearSavedView}
                    onResetToSaved={handleResetToSaved}
                    onSavedViewUpdated={savedViewsState.handleSavedViewUpdated}
                    savedViews={savedViewsState.savedViews}
                    setAutoFillColumnId={savedViewsState.setAutoFillColumnId}
                    setWrappedColumnIds={savedViewsState.setWrappedColumnIds}
                    {showChart}
                    {showStats}
                    setShowChart={(value) => (showChart = value)}
                    setShowStats={(value) => (showStats = value)}
                    sort={getEffectiveSort() ?? undefined}
                    {table}
                    time={getQueryTime() ?? undefined}
                    view={VIEW}
                    wrappedColumnIds={savedViewsState.wrappedColumnIds}
                />
            {/if}
            <RefreshButton onRefresh={handleRefresh} isRefreshing={sessionsQuery.isFetching} size="icon-lg" title="Refresh results" />
        </div>
    </div>

    <div class="flex flex-col gap-y-4" class:opacity-60={!hasPremiumFeatures}>
        {#if showStats}
            <SessionsStatsDashboard
                avgDuration={stats.avgDuration}
                avgPerHour={stats.avgPerHour}
                isLoading={isSavedViewRoutePending || (statsQuery.isLoading && !statsQuery.isSuccess)}
                totalSessions={stats.totalSessions}
                totalUsers={stats.totalUsers}
            />
        {/if}

        {#if showChart}
            <SessionsDashboardChart data={chartData} isLoading={isSavedViewRoutePending || (statsQuery.isLoading && !statsQuery.isSuccess)} {onRangeSelect} />
        {/if}

        <EventsDataTable
            autoFillColumnId={savedViewsState.autoFillColumnId}
            bind:limit={eventsQueryParameters.limit!}
            isLoading={isSavedViewRoutePending || sessionsQuery.isFetching}
            onAutoFillColumnResized={() => savedViewsState.setAutoFillColumnId(null)}
            {rowClick}
            {rowHref}
            {table}
            wrappedColumnIds={savedViewsState.wrappedColumnIds}
        >
            {#snippet footerChildren()}
                <DataTable.Selection {table} />
                <DataTable.Pager bind:value={eventsQueryParameters.limit!} {table} variant="floating" />
            {/snippet}
        </EventsDataTable>
    </div>
</div>

<EventDetailSheet bind:eventId={selectedEventId} filterChanged={onFilterChanged} onClose={() => (selectedEventId = null)} />
