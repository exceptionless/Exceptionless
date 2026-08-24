<script lang="ts">
    import type { GetEventsParams } from '$features/events/api.svelte';

    import { page } from '$app/state';
    import * as DataTable from '$comp/data-table';
    import DelayedRender from '$comp/delayed-render.svelte';
    import ErrorMessage from '$comp/error-message.svelte';
    import * as FacetedFilter from '$comp/faceted-filter';
    import StreamingIndicatorButton from '$comp/streaming-indicator-button.svelte';
    import { H3 } from '$comp/typography';
    import { showBillingDialogOnUpgradeProblem } from '$features/billing/upgrade-required.svelte';
    import { PERSISTENT_EVENT_DELETE_RECONCILE_EVENT } from '$features/events/api.svelte';
    import EventDetailSheet from '$features/events/components/event-detail-sheet.svelte';
    import { ProjectFilter, StatusFilter, TagFilter } from '$features/events/components/filters';
    import {
        buildFilterCacheKey,
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
    import { buildEventDetailsHref, type EventSummaryModel, type SummaryTemplateKeys } from '$features/events/components/summary/index';
    import { defaultEventColumnVisibility, getColumns } from '$features/events/components/table/options.svelte';
    import { organization } from '$features/organizations/context.svelte';
    import SavedViewPicker from '$features/saved-views/components/saved-view-picker.svelte';
    import { isSavedViewHydrationPending, useSavedViews } from '$features/saved-views/use-saved-views.svelte';
    import { getSharedTableOptions, isTableEmpty, removeTableData } from '$features/shared/table.svelte';
    import { StackStatus } from '$features/stacks/models';
    import { ChangeType, type WebSocketMessageValue } from '$features/websockets/models';
    import { DEFAULT_LIMIT, DEFAULT_OFFSET, useFetchClientStatus } from '$shared/api/api.svelte';
    import { createQueryParameters } from '$shared/query-params';
    import { type FetchClientResponse, type ProblemDetails, useFetchClient } from '@foundatiofx/fetchclient';
    import { createTable } from '@tanstack/svelte-table';
    import { useEventListener, watch } from 'runed';
    import { onDestroy } from 'svelte';
    import { debounce } from 'throttle-debounce';

    import { getEventsNavigationOptionsForFilter, redirectToEventsWithFilter } from '../redirect-to-events.svelte';

    let selectedEventId: null | string = $state(null);

    function handleEventError(problem: ProblemDetails) {
        showBillingDialogOnUpgradeProblem(problem, organization.current);
        selectedEventId = null;
    }

    function rowclick(row: EventSummaryModel<SummaryTemplateKeys>) {
        selectedEventId = row.id;
    }

    function rowHref(row: EventSummaryModel<SummaryTemplateKeys>): string {
        return buildEventDetailsHref(row.id);
    }

    const DEFAULT_FILTERS = [new ProjectFilter([]), new StatusFilter([StackStatus.Open, StackStatus.Regressed])];
    const DEFAULT_PARAMS = {
        filter: '(status:open OR status:regressed)',
        limit: DEFAULT_LIMIT,
        saved: undefined as string | undefined
    };

    function filterCacheKey(filter: null | string): string {
        return buildFilterCacheKey(organization.current, page.url.pathname, filter);
    }

    updateFilterCache(filterCacheKey(DEFAULT_PARAMS.filter), DEFAULT_FILTERS);
    const queryParams = createQueryParameters({
        defaults: DEFAULT_PARAMS,
        history: 'push',
        schema: {
            filter: 'string',
            limit: 'number',
            saved: 'string'
        }
    });

    const VIEW = 'stream';
    const savedViewsState = useSavedViews({
        applyFilters: (draftFilters) => {
            updateFilters(draftFilters);
            filters = draftFilters;
        },
        defaultAutoFillColumnId: 'summary',
        defaultColumnVisibility: defaultEventColumnVisibility,
        defaultFilter: DEFAULT_PARAMS.filter,
        filterCacheKey,
        getColumnOrder: () => table.store.state.columnOrder,
        getColumnSizing: () => table.store.state.columnSizing,
        getColumnVisibility: () => table.store.state.columnVisibility,
        getFilterDefinitions: () => serializeFilters(filters ?? []),
        queryParams,
        setColumnOrder: (v) => table.setColumnOrder(v),
        setColumnSizing: (v) => table.setColumnSizing(v),
        setColumnVisibility: (v) => table.setColumnVisibility(v),
        updateFilterCache,
        view: VIEW
    });
    const isSavedViewPending = $derived(
        isSavedViewHydrationPending(
            queryParams.saved,
            savedViewsState.activeSavedView?.id,
            savedViewsState.hydratedSavedViewId,
            savedViewsState.isMissing || savedViewsState.isError
        )
    );
    const pageTitle = $derived(savedViewsState.activeSavedView?.name ?? 'Event Stream');

    $effect(() => {
        document.title = `${pageTitle} - Exceptionless`;
    });

    watch(
        () => organization.current,
        () => {
            updateFilterCache(filterCacheKey(DEFAULT_PARAMS.filter), DEFAULT_FILTERS);
            queryParams.update(DEFAULT_PARAMS);
            paused = false;
        },
        {
            lazy: true
        }
    );

    let filters = $state(getFiltersFromCache(filterCacheKey(queryParams.filter), queryParams.filter));
    watch(
        [() => queryParams.filter],
        ([filter]) => {
            filters = getFiltersFromCache(filterCacheKey(filter), filter);
        },
        {
            lazy: true
        }
    );

    async function onFilterChanged(addedOrUpdated: FacetedFilter.IFilter) {
        // If this is a stack filter, redirect to the Events page
        if (addedOrUpdated.type === 'string' && addedOrUpdated.key === 'string-stack') {
            await redirectToEventsWithFilter(organization.current, addedOrUpdated, getEventsNavigationOptionsForFilter(addedOrUpdated));
            return;
        }

        // For all other filters (skipping date filters), apply them to the current page
        if (addedOrUpdated.type !== 'date') {
            const isNew = !filters?.some((f) => f.id === addedOrUpdated.id);
            const updatedFilters = filterChanged(filters ?? [], addedOrUpdated);
            updateFilters(updatedFilters);
            if (isNew) {
                filters = updatedFilters;
            }
        }

        selectedEventId = null;
    }

    function onFilterRemoved(removed?: FacetedFilter.IFilter): void {
        const updatedFilters = filterRemoved(filters ?? [], removed);
        updateFilters(updatedFilters);
        filters = updatedFilters;
    }

    function updateFilters(updatedFilters: FacetedFilter.IFilter[]): void {
        const filter = toFilter(updatedFilters);
        updateFilterCache(filterCacheKey(filter), updatedFilters);
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
        mode: 'summary',
        offset: DEFAULT_OFFSET
    });

    const client = useFetchClient();
    const clientStatus = useFetchClientStatus(client);
    let clientResponse = $state<FetchClientResponse<EventSummaryModel<SummaryTemplateKeys>[]>>();
    let before = $state<string | undefined>(undefined);
    let queryData = $state<EventSummaryModel<SummaryTemplateKeys>[]>([]);

    const table = createTable(
        getSharedTableOptions<EventSummaryModel<SummaryTemplateKeys>>({
            columnPersistenceKey: 'stream-column-visibility',
            get columns() {
                return getColumns<EventSummaryModel<SummaryTemplateKeys>>(eventsQueryParameters.mode, {
                    onTagClick: (tag) => onFilterChanged(new TagFilter([tag])),
                    showType: !hasSingleTypeFilter(eventsQueryParameters.filter)
                })
                    .filter((c) => c.id !== 'select')
                    .map((c) => ({
                        ...c,
                        enableSorting: false
                    }));
            },
            configureOptions: (options) => {
                options.enableMultiRowSelection = false;
                options.enableRowSelection = false;
                options.manualSorting = false;
                return options;
            },
            defaultColumnVisibility: defaultEventColumnVisibility,
            enableColumnResizing: true,
            paginationStrategy: 'cursor',
            get queryData() {
                return queryData;
            },
            get queryMeta() {
                return clientResponse?.meta;
            },
            get queryParameters() {
                return eventsQueryParameters;
            }
        })
    );

    let loadDataRequestId = 0;
    let paused = $state(false);
    function handleToggle() {
        paused = !paused;
        if (paused) {
            loadDataRequestId++;
        }
    }

    async function loadData(filterChanged: boolean = false) {
        if (isSavedViewPending) {
            loadDataRequestId++;
            before = undefined;
            clientResponse = undefined;
            queryData = [];
            return;
        }

        if (client.isLoading && filterChanged && !before) {
            return;
        }

        const requestId = ++loadDataRequestId;
        if (paused) {
            return;
        }

        if (!organization.current) {
            return;
        }

        if (filterChanged) {
            before = undefined;
        }

        const response = await client.getJSON<EventSummaryModel<SummaryTemplateKeys>[]>(`organizations/${organization.current}/events`, {
            expectedStatusCodes: [426],
            params: {
                ...eventsQueryParameters,
                before
            }
        });
        if (requestId !== loadDataRequestId) {
            return;
        }

        clientResponse = response;

        if (clientResponse.problem && showBillingDialogOnUpgradeProblem(clientResponse.problem, organization.current, () => loadData(true))) {
            return;
        }

        if (clientResponse.ok) {
            if (clientResponse.meta.links.previous?.before) {
                before = clientResponse.meta.links.previous?.before;
            }

            const data = filterChanged ? [] : queryData;
            for (const summary of clientResponse.data?.reverse() || []) {
                data.push(summary);
            }

            queryData = data.slice(-queryParams.limit!);
        }
    }

    const debouncedLoadData = debounce(5000, loadData);
    onDestroy(() => {
        loadDataRequestId++;
        debouncedLoadData.cancel();
    });
    function onPersistentEventChanged(message: WebSocketMessageValue<'PersistentEventChanged'>) {
        if (message.id && message.change_type === ChangeType.Removed) {
            if (removeTableData(table, (doc) => doc.id === message.id)) {
                // If the grid data is empty from all events being removed, we should refresh the data.
                if (isTableEmpty(table) && !paused) {
                    debouncedLoadData();
                    return;
                }
            }
        }

        if (paused) {
            return;
        }

        // Do not refresh if the filter criteria doesn't match the web socket message.
        if (!shouldRefreshPersistentEventChanged(filters, queryParams.filter, message.organization_id, message.project_id, message.stack_id, message.id)) {
            return;
        }

        debouncedLoadData();
    }

    useEventListener(document, PERSISTENT_EVENT_DELETE_RECONCILE_EVENT, () => debouncedLoadData(true));
    useEventListener(document, 'refresh', () => loadData(true));
    useEventListener(document, 'PersistentEventChanged', (event) => onPersistentEventChanged((event as CustomEvent).detail));

    $effect(() => {
        // Handle case where pop state loses the limit
        queryParams.limit ??= DEFAULT_LIMIT;
    });

    $effect(() => {
        if (!isSavedViewPending) {
            return;
        }

        loadDataRequestId++;
        before = undefined;
        clientResponse = undefined;
        queryData = [];
    });

    $effect(() => {
        if (paused) {
            return;
        }

        loadData();
    });
</script>

<DataTable.Root>
    <div class="mb-4 flex flex-wrap items-start gap-2">
        <H3 class="my-0 shrink-0">{pageTitle}</H3>
        <div class="order-3 flex w-full flex-wrap items-start gap-1.5 md:order-none md:w-auto md:min-w-0 md:flex-1">
            <FacetedFilter.Root changed={onFilterChanged} {filters} remove={onFilterRemoved}>
                <OrganizationDefaultsFacetedFilterBuilder />
            </FacetedFilter.Root>
        </div>
        <div class="ml-auto flex shrink-0 items-start gap-2">
            {#if savedViewsState.isEnabled}
                <SavedViewPicker
                    activeSavedView={savedViewsState.activeSavedView}
                    autoFillColumnId={savedViewsState.autoFillColumnId}
                    columnOrder={table.store.state.columnOrder}
                    columnSizing={table.store.state.columnSizing}
                    columnVisibility={table.store.state.columnVisibility}
                    defaultAutoFillColumnId="summary"
                    filters={filters ?? []}
                    isModified={savedViewsState.isModified}
                    onLoadView={savedViewsState.handleLoadView}
                    onClearSavedView={savedViewsState.handleClearSavedView}
                    onResetToSaved={savedViewsState.handleResetToSaved}
                    savedViews={savedViewsState.savedViews}
                    setAutoFillColumnId={savedViewsState.setAutoFillColumnId}
                    setWrappedColumnIds={savedViewsState.setWrappedColumnIds}
                    {table}
                    view={VIEW}
                    wrappedColumnIds={savedViewsState.wrappedColumnIds}
                />
            {/if}
            <StreamingIndicatorButton onToggle={handleToggle} {paused} size="icon-lg" />
        </div>
    </div>
    <DataTable.Footer {table}>
        <div class="flex w-full items-center justify-center gap-4">
            <DataTable.PageSize bind:value={queryParams.limit!} {table} />
            <div class="text-center">
                <ErrorMessage message={clientResponse?.problem?.errors.general} />
            </div>
        </div>
    </DataTable.Footer>
    <DataTable.Body
        autoFillColumnId={savedViewsState.autoFillColumnId}
        rowClick={rowclick}
        {rowHref}
        {table}
        wrappedColumnIds={savedViewsState.wrappedColumnIds}
    >
        {#if isSavedViewPending || clientStatus.isLoading}
            <DelayedRender>
                <DataTable.Loading {table} />
            </DelayedRender>
        {:else}
            <DataTable.Empty {table} />
        {/if}
    </DataTable.Body>
</DataTable.Root>

<EventDetailSheet bind:eventId={selectedEventId} filterChanged={onFilterChanged} onClose={() => (selectedEventId = null)} onError={handleEventError} />
