<script lang="ts">
    import type { Stack } from '$features/stacks/models';

    import { resolve } from '$app/paths';
    import { page } from '$app/state';
    import * as DataTable from '$comp/data-table';
    import DataTableViewOptions from '$comp/data-table/data-table-view-options.svelte';
    import * as FacetedFilter from '$comp/faceted-filter';
    import RefreshButton from '$comp/refresh-button.svelte';
    import { Muted } from '$comp/typography';
    import { showBillingDialogOnUpgradeProblem } from '$features/billing/upgrade-required.svelte';
    import { StatusFilter, StringFilter, TagFilter } from '$features/events/components/filters';
    import {
        buildFilterCacheKey,
        filterCacheVersionNumber,
        filterChanged,
        filterRemoved,
        getFiltersFromCache,
        toFilter,
        updateFilterCache
    } from '$features/events/components/filters/helpers.svelte';
    import { getSharedTableOptions, removeTableSelection } from '$features/shared/table.svelte';
    import StackFacetedFilterBuilder from '$features/stacks/components/filters/stack-faceted-filter-builder.svelte';
    import TableStacksBulkActionsDropdownMenu from '$features/stacks/components/stacks-bulk-actions-dropdown-menu.svelte';
    import { getColumns } from '$features/stacks/components/table/options.svelte';
    import StacksDataTable from '$features/stacks/components/table/stacks-data-table.svelte';
    import { StackStatus } from '$features/stacks/models';
    import { describeStackFilter, isStackFilterSupported, splitSupportedStackFilters } from '$features/stacks/stack-filter-support';
    import { ChangeType, type WebSocketMessageValue } from '$features/websockets/models';
    import { DEFAULT_LIMIT, useFetchClientStatus } from '$shared/api/api.svelte';
    import { type FetchClientResponse, useFetchClient } from '@exceptionless/fetchclient';
    import { createTable } from '@tanstack/svelte-table';
    import { queryParamsState } from 'kit-query-params';
    import { useEventListener, watch } from 'runed';
    import { toast } from 'svelte-sonner';
    import { throttle } from 'throttle-debounce';

    const projectId = $derived(page.params.projectId);
    const DEFAULT_PARAMS = {
        filter: '(status:ignored OR status:discarded)',
        limit: DEFAULT_LIMIT,
        page: 1,
        sort: '-last'
    };

    const DEFAULT_FILTERS = [new StatusFilter([StackStatus.Ignored, StackStatus.Discarded])];

    function filterCacheKey(filter: null | string): string {
        return buildFilterCacheKey(projectId, page.url.pathname, filter);
    }

    updateFilterCache(filterCacheKey(DEFAULT_PARAMS.filter), DEFAULT_FILTERS);
    const queryParams = queryParamsState({
        default: DEFAULT_PARAMS,
        pushHistory: true,
        schema: {
            filter: 'string',
            limit: 'number',
            page: 'number',
            sort: 'string'
        }
    });

    watch(
        () => projectId,
        () => {
            updateFilterCache(filterCacheKey(DEFAULT_PARAMS.filter), DEFAULT_FILTERS);
            Object.assign(queryParams, DEFAULT_PARAMS);
            reset();
        },
        { lazy: true }
    );

    function normalizeStackKeywordFilters(nextFilters: FacetedFilter.IFilter[]): FacetedFilter.IFilter[] {
        if (nextFilters.length !== 1 || nextFilters[0]?.type !== 'keyword') {
            return nextFilters;
        }

        const filter = nextFilters[0] as { value?: string };
        const keywordValue = filter.value?.trim();
        if (!keywordValue) {
            return nextFilters;
        }

        const stackMatch = /^stack:"?([^"\s]+)"?$/i.exec(keywordValue);
        if (!stackMatch?.[1]) {
            return nextFilters;
        }

        return [new StringFilter('stack', stackMatch[1])];
    }

    function sanitizeStackFilters(nextFilters: FacetedFilter.IFilter[], notify = false): FacetedFilter.IFilter[] {
        const normalizedFilters = normalizeStackKeywordFilters(nextFilters);
        const { supported, unsupported } = splitSupportedStackFilters(normalizedFilters);
        if (unsupported.length === 0) {
            return normalizedFilters;
        }

        const sanitizedFilter = toFilter(supported);
        if (queryParams.filter !== sanitizedFilter) {
            queryParams.filter = sanitizedFilter;
        }

        updateFilterCache(filterCacheKey(sanitizedFilter), supported);

        if (notify) {
            const removed = unsupported.map((filter) => describeStackFilter(filter)).join(', ');
            toast.error(`Removed unsupported stack filters: ${removed}.`);
        }

        return supported;
    }

    let filters = $state(sanitizeStackFilters(getFiltersFromCache(filterCacheKey(queryParams.filter), queryParams.filter)));
    watch(
        [() => queryParams.filter, () => filterCacheVersionNumber()],
        ([filter]) => {
            filters = sanitizeStackFilters(getFiltersFromCache(filterCacheKey(filter), filter), true);
        },
        { lazy: true }
    );

    $effect(() => {
        queryParams.limit ??= DEFAULT_LIMIT;
        queryParams.page ??= 1;
        queryParams.sort ??= '-last';
    });

    function onFilterChanged(addedOrUpdated: FacetedFilter.IFilter) {
        if (!isStackFilterSupported(addedOrUpdated)) {
            toast.error(`"${describeStackFilter(addedOrUpdated)}" is not supported in issue management.`);
            return;
        }

        updateFilters(filterChanged(filters ?? [], addedOrUpdated));
    }

    function onFilterRemoved(removed?: FacetedFilter.IFilter): void {
        if (!removed) {
            updateFilters([]);
            return;
        }

        updateFilters(filterRemoved(filters ?? [], removed));
    }

    function updateFilters(updatedFilters: FacetedFilter.IFilter[]): void {
        const sanitizedFilters = sanitizeStackFilters(updatedFilters);
        const filter = toFilter(sanitizedFilters);
        updateFilterCache(filterCacheKey(filter), sanitizedFilters);
        queryParams.page = 1;
        queryParams.filter = filter;
    }

    function handleTagClick(tag: string) {
        onFilterChanged(new TagFilter([tag]));
    }

    interface StacksQueryParameters {
        filter?: string;
        limit?: number;
        page?: number;
        sort?: string;
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
        get page() {
            return queryParams.page!;
        },
        set page(value) {
            queryParams.page = value;
        },
        get sort() {
            return queryParams.sort!;
        },
        set sort(value) {
            queryParams.sort = value;
        }
    });

    const client = useFetchClient();
    const clientStatus = useFetchClientStatus(client);
    let clientResponse = $state<FetchClientResponse<Stack[]>>();

    function rowHref(row: Stack): string {
        return resolve('/(app)/project/[projectId]/issues/[stackId]', { projectId: projectId ?? '', stackId: row.id });
    }

    const table = createTable(
        getSharedTableOptions<Stack>({
            columnPersistenceKey: 'project-issues-v2-column-visibility',
            get columns() {
                return getColumns(handleTagClick);
            },
            defaultColumnVisibility: {
                critical: false,
                events: true,
                first: false,
                fixed_in_version: false,
                last: true,
                select: true,
                status: false,
                tags: false,
                title: true,
                type: true
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

    async function loadData(filter = queryParams.filter, limit = queryParams.limit, pageNumber = queryParams.page, sort = queryParams.sort) {
        if (!projectId) {
            return;
        }

        clientResponse = await client.getJSON<Stack[]>(`projects/${projectId}/stacks`, {
            params: {
                filter,
                limit,
                page: pageNumber,
                sort
            }
        });

        showBillingDialogOnUpgradeProblem(clientResponse.problem, undefined);
    }

    const throttledLoadData = throttle(5000, loadData);

    async function onStackChanged(message: WebSocketMessageValue<'StackChanged'>) {
        if (message.id && message.change_type === ChangeType.Removed) {
            removeTableSelection(table, message.id);
            await loadData();
            return;
        }

        await throttledLoadData();
    }

    useEventListener(document, 'StackChanged', async (event) => await onStackChanged((event as CustomEvent).detail));

    $effect(() => {
        loadData(queryParams.filter, queryParams.limit, queryParams.page, queryParams.sort);
    });
</script>

<div class="flex flex-col">
    <div class="mb-4 flex flex-wrap items-start gap-2">
        <Muted class="w-full shrink-0">Manage project issues, including restoring ignored or discarded issues.</Muted>
        <div class="flex min-w-0 flex-1 flex-wrap items-start gap-2">
            <FacetedFilter.Root changed={onFilterChanged} {filters} remove={onFilterRemoved}>
                <StackFacetedFilterBuilder includeProject={false} />
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

    <StacksDataTable bind:limit={queryParams.limit!} isLoading={clientStatus.isLoading} {rowHref} {table}>
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
