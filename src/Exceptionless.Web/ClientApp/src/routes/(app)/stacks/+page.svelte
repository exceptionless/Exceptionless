<script lang="ts">
    import type { Stack } from '$features/stacks/models';

    import { page } from '$app/state';
    import * as DataTable from '$comp/data-table';
    import DataTableViewOptions from '$comp/data-table/data-table-view-options.svelte';
    import * as FacetedFilter from '$comp/faceted-filter';
    import RefreshButton from '$comp/refresh-button.svelte';
    import { H3 } from '$comp/typography';
    import { DateFilter, ProjectFilter, StatusFilter } from '$features/events/components/filters';
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
    import { organization } from '$features/organizations/context.svelte';
    import { getSharedTableOptions, isTableEmpty, removeTableData, removeTableSelection } from '$features/shared/table.svelte';
    import TableStacksBulkActionsDropdownMenu from '$features/stacks/components/stacks-bulk-actions-dropdown-menu.svelte';
    import { getColumns } from '$features/stacks/components/table/options.svelte';
    import StacksDataTable from '$features/stacks/components/table/stacks-data-table.svelte';
    import { ChangeType, type WebSocketMessageValue } from '$features/websockets/models';
    import { DEFAULT_LIMIT, useFetchClientStatus } from '$shared/api/api.svelte';
    import { type FetchClientResponse, useFetchClient } from '@exceptionless/fetchclient';
    import { createTable } from '@tanstack/svelte-table';
    import { queryParamsState } from 'kit-query-params';
    import { useEventListener, watch } from 'runed';
    import { throttle } from 'throttle-debounce';

    const DEFAULT_PARAMS = {
        filter: '',
        limit: DEFAULT_LIMIT,
        time: undefined as string | undefined
    };

    const DEFAULT_FILTERS = [new DateFilter('date', undefined), new ProjectFilter([]), new StatusFilter([])];

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

    // Reset on organization change
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

    function onFilterChanged(addedOrUpdated: FacetedFilter.IFilter) {
        updateFilters(filterChanged(filters ?? [], addedOrUpdated));
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

    interface StacksQueryParameters {
        filter?: string;
        limit?: number;
        page?: number;
        time?: string;
    }

    const stacksQueryParameters: StacksQueryParameters = $state({
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
        page: undefined,
        get time() {
            return queryParams.time!;
        },
        set time(value) {
            queryParams.time = value;
        }
    });

    const client = useFetchClient();
    const clientStatus = useFetchClientStatus(client);
    let clientResponse = $state<FetchClientResponse<Stack[]>>();

    const table = createTable(
        getSharedTableOptions<Stack>({
            columnPersistenceKey: 'stacks-column-visibility',
            get columns() {
                return getColumns();
            },
            paginationStrategy: 'offset',
            get queryData() {
                return clientResponse?.data ?? [];
            },
            get queryMeta() {
                return clientResponse?.meta;
            },
            get queryParameters() {
                return stacksQueryParameters;
            }
        })
    );

    const canRefresh = $derived(!table.getIsSomeRowsSelected() && !table.getIsAllRowsSelected() && table.store.state.pagination.pageIndex === 0);

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

        clientResponse = await client.getJSON<Stack[]>(`organizations/${organization.current}/stacks`, {
            params: stacksQueryParameters as Record<string, unknown>
        });
    }

    const throttledLoadData = throttle(5000, loadData);

    async function onStackChanged(message: WebSocketMessageValue<'StackChanged'>) {
        if (message.id && message.change_type === ChangeType.Removed) {
            removeTableSelection(table, message.id);

            if (removeTableData(table, (doc: Stack) => doc.id === message.id)) {
                if (isTableEmpty(table)) {
                    await throttledLoadData();
                    return;
                }
            }
        }

        // Refresh data on any other stack change
        await throttledLoadData();
    }

    useEventListener(document, 'StackChanged', async (event) => await onStackChanged((event as CustomEvent).detail));

    $effect(() => {
        loadData();
    });
</script>

<div class="flex flex-col">
    <div class="mb-4 flex flex-wrap items-start gap-2">
        <H3 class="my-0 shrink-0">Stacks</H3>
        <div class="flex min-w-0 flex-1 flex-wrap items-start gap-2">
            <FacetedFilter.Root changed={onFilterChanged} {filters} remove={onFilterRemoved}>
                <OrganizationDefaultsFacetedFilterBuilder />
            </FacetedFilter.Root>
        </div>
        <div class="ml-auto flex shrink-0 items-start gap-2">
            <RefreshButton
                onRefresh={handleRefresh}
                isRefreshing={clientStatus.isLoading}
                size="icon-lg"
                title={canRefresh ? 'Refresh results' : 'Return to the first page to refresh results'}
            />
            <DataTableViewOptions size="icon-lg" {table} />
        </div>
    </div>

    <StacksDataTable bind:limit={queryParams.limit!} isLoading={clientStatus.isLoading} {table}>
        {#snippet footerChildren()}
            <div class="h-9 min-w-35">
                <TableStacksBulkActionsDropdownMenu {table} />
            </div>

            <DataTable.Selection {table} />
            <DataTable.PageSize bind:value={queryParams.limit!} {table}></DataTable.PageSize>
            <div class="flex items-center space-x-6 lg:space-x-8">
                <DataTable.PageCount {table} />
                <DataTable.Pagination {table} />
            </div>
        {/snippet}
    </StacksDataTable>
</div>
