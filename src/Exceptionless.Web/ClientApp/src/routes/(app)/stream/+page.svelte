<script lang="ts">
    import type { GetEventsParams } from '$features/events/api.svelte';
    import type { EventSummaryModel, SummaryTemplateKeys } from '$features/events/components/summary/index';

    import { resolve } from '$app/paths';
    import { page } from '$app/state';
    import * as DataTable from '$comp/data-table';
    import DelayedRender from '$comp/delayed-render.svelte';
    import ErrorMessage from '$comp/error-message.svelte';
    import * as FacetedFilter from '$comp/faceted-filter';
    import StreamingIndicatorButton from '$comp/streaming-indicator-button.svelte';
    import { H3 } from '$comp/typography';
    import { showBillingDialogOnUpgradeProblem } from '$features/billing/upgrade-required.svelte';
    import EventDetailSheet from '$features/events/components/event-detail-sheet.svelte';
    import { ProjectFilter, StatusFilter } from '$features/events/components/filters';
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
    import { defaultEventColumnVisibility, getColumns } from '$features/events/components/table/options.svelte';
    import { organization } from '$features/organizations/context.svelte';
    import SavedViewPicker from '$features/saved-views/components/saved-view-picker.svelte';
    import { useSavedViews } from '$features/saved-views/use-saved-views.svelte';
    import { getSharedTableOptions, isTableEmpty, removeTableData } from '$features/shared/table.svelte';
    import { StackStatus } from '$features/stacks/models';
    import { ChangeType, type WebSocketMessageValue } from '$features/websockets/models';
    import { DEFAULT_LIMIT, DEFAULT_OFFSET, useFetchClientStatus } from '$shared/api/api.svelte';
    import { type FetchClientResponse, type ProblemDetails, useFetchClient } from '@exceptionless/fetchclient';
    import { createTable } from '@tanstack/svelte-table';
    import { queryParamsState } from 'kit-query-params';
    import { useEventListener, watch } from 'runed';
    import { debounce } from 'throttle-debounce';

    import { redirectToEventsWithFilter } from '../redirect-to-events.svelte';

    let selectedEventId: null | string = $state(null);

    function handleEventError(problem: ProblemDetails) {
        showBillingDialogOnUpgradeProblem(problem, organization.current);
        selectedEventId = null;
    }

    function rowclick(row: EventSummaryModel<SummaryTemplateKeys>) {
        selectedEventId = row.id;
    }

    function rowHref(row: EventSummaryModel<SummaryTemplateKeys>): string {
        return resolve('/(app)/event/[eventId=objectid]', { eventId: row.id });
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
    const queryParams = queryParamsState({
        default: DEFAULT_PARAMS,
        pushHistory: true,
        schema: {
            filter: 'string',
            limit: 'number',
            saved: 'string'
        }
    });

    const VIEW = 'stream';
    const savedViewsState = useSavedViews({
        defaultColumnVisibility: defaultEventColumnVisibility,
        filterCacheKey,
        getColumnOrder: () => table.state.columnOrder,
        getColumnVisibility: () => table.state.columnVisibility,
        getFilterDefinitions: () => serializeFilters(filters ?? []),
        queryParams,
        setColumnOrder: (v) => table.setColumnOrder(v),
        setColumnVisibility: (v) => table.setColumnVisibility(v),
        updateFilterCache,
        view: VIEW
    });
    const pageTitle = $derived(savedViewsState.activeSavedView?.name ?? 'Event Stream');

    $effect(() => {
        document.title = `${pageTitle} - Exceptionless`;
    });

    watch(
        () => organization.current,
        () => {
            updateFilterCache(filterCacheKey(DEFAULT_PARAMS.filter), DEFAULT_FILTERS);
            //params.$reset(); // Work around for https://github.com/beynar/kit-query-params/issues/7
            Object.assign(queryParams, DEFAULT_PARAMS);
            paused = false;
        },
        { lazy: true }
    );

    let filters = $state(getFiltersFromCache(filterCacheKey(queryParams.filter), queryParams.filter));
    watch(
        [() => queryParams.filter],
        ([filter]) => {
            filters = getFiltersFromCache(filterCacheKey(filter), filter);
        },
        { lazy: true }
    );

    async function onFilterChanged(addedOrUpdated: FacetedFilter.IFilter) {
        // If this is a stack filter, redirect to the Events page
        if (addedOrUpdated.type === 'string' && addedOrUpdated.key === 'string-stack') {
            await redirectToEventsWithFilter(organization.current, addedOrUpdated, { time: null });
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
                    showType: !hasSingleTypeFilter(eventsQueryParameters.filter)
                })
                    .filter((c) => c.id !== 'select')
                    .map((c) => ({ ...c, enableSorting: false }));
            },
            configureOptions: (options) => {
                options.enableMultiRowSelection = false;
                options.enableRowSelection = false;
                options.manualSorting = false;
                return options;
            },
            defaultColumnVisibility: defaultEventColumnVisibility,
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
        }),
        (state) => ({
            columnOrder: state.columnOrder,
            columnVisibility: state.columnVisibility
        })
    );

    let paused = $state(false);
    function handleToggle() {
        paused = !paused;
    }

    async function loadData(filterChanged: boolean = false) {
        if (paused) {
            return;
        }

        if (!organization.current) {
            return;
        }

        if (client.isLoading && filterChanged && !before) {
            return;
        }

        if (filterChanged) {
            before = undefined;
        }

        clientResponse = await client.getJSON<EventSummaryModel<SummaryTemplateKeys>[]>(`organizations/${organization.current}/events`, {
            params: {
                ...eventsQueryParameters,
                before
            }
        });

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
    async function onPersistentEventChanged(message: WebSocketMessageValue<'PersistentEventChanged'>) {
        if (message.id && message.change_type === ChangeType.Removed) {
            if (removeTableData(table, (doc) => doc.id === message.id)) {
                // If the grid data is empty from all events being removed, we should refresh the data.
                if (isTableEmpty(table) && !paused) {
                    await debouncedLoadData();
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

        await debouncedLoadData();
    }

    useEventListener(document, 'refresh', async () => await loadData());
    useEventListener(document, 'PersistentEventChanged', (event) => onPersistentEventChanged((event as CustomEvent).detail));

    $effect(() => {
        // Handle case where pop state loses the limit
        queryParams.limit ??= DEFAULT_LIMIT;
    });

    $effect(() => {
        loadData();
    });
</script>

<DataTable.Root>
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
                    {table}
                    view={VIEW}
                />
            {/if}
            <StreamingIndicatorButton onToggle={handleToggle} {paused} size="icon-lg" />
        </div>
    </div>
    <DataTable.Body rowClick={rowclick} {rowHref} {table}>
        {#if clientStatus.isLoading}
            <DelayedRender>
                <DataTable.Loading {table} />
            </DelayedRender>
        {:else}
            <DataTable.Empty {table} />
        {/if}
    </DataTable.Body>
    <DataTable.Footer {table}>
        <div class="flex w-full items-center justify-center space-x-4">
            <DataTable.PageSize bind:value={queryParams.limit!} {table} />
            <div class="text-center">
                <ErrorMessage message={clientResponse?.problem?.errors.general} />
            </div>
        </div>
    </DataTable.Footer>
</DataTable.Root>

<EventDetailSheet eventId={selectedEventId} filterChanged={onFilterChanged} onClose={() => (selectedEventId = null)} onError={handleEventError} />
