<script lang="ts">
    import type { EventSummaryModel, SummaryTemplateKeys } from '$features/events/components/summary/index';

    import { page } from '$app/state';
    import * as DataTable from '$comp/data-table';
    import DelayedRender from '$comp/delayed-render.svelte';
    import ErrorMessage from '$comp/error-message.svelte';
    import * as FacetedFilter from '$comp/faceted-filter';
    import { Button } from '$comp/ui/button';
    import * as Card from '$comp/ui/card';
    import * as Sheet from '$comp/ui/sheet';
    import EventsOverview from '$features/events/components/events-overview.svelte';
    import { StatusFilter } from '$features/events/components/filters';
    import {
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
    import { getTableContext } from '$features/events/components/table/options.svelte';
    import { organization } from '$features/organizations/context.svelte';
    import { isTableEmpty, removeTableData } from '$features/shared/table.svelte';
    import { StackStatus } from '$features/stacks/models';
    import { ChangeType, type WebSocketMessageValue } from '$features/websockets/models';
    import { DEFAULT_LIMIT, useFetchClientStatus } from '$shared/api/api.svelte';
    import { type FetchClientResponse, useFetchClient } from '@exceptionless/fetchclient';
    import { createTable } from '@tanstack/svelte-table';
    import { queryParamsState } from 'kit-query-params';
    import ExternalLink from 'lucide-svelte/icons/external-link';
    import { useEventListener, watch } from 'runed';
    import { debounce } from 'throttle-debounce';

    let selectedEventId: null | string = $state(null);
    function rowclick(row: EventSummaryModel<SummaryTemplateKeys>) {
        selectedEventId = row.id;
    }

    const DEFAULT_FILTERS = [new StatusFilter([StackStatus.Open, StackStatus.Regressed])];
    const DEFAULT_PARAMS = {
        filter: '(status:open OR status:regressed)',
        limit: DEFAULT_LIMIT
    };

    function filterCacheKey(filter: null | string): string {
        return buildFilterCacheKey(organization.current, page.url.pathname, filter);
    }

    updateFilterCache(filterCacheKey(DEFAULT_PARAMS.filter), DEFAULT_FILTERS);
    const params = queryParamsState({
        default: DEFAULT_PARAMS,
        pushHistory: true,
        schema: {
            filter: 'string',
            limit: 'number'
        }
    });

    watch(
        () => organization.current,
        () => {
            updateFilterCache(filterCacheKey(DEFAULT_PARAMS.filter), DEFAULT_FILTERS);
            //params.$reset(); // Work around for https://github.com/beynar/kit-query-params/issues/7
            Object.assign(params, DEFAULT_PARAMS);
        },
        { lazy: true }
    );

    let filters = $state(getFiltersFromCache(filterCacheKey(params.filter), params.filter));
    watch(
        [() => params.filter, () => filterCacheVersionNumber()],
        ([filter]) => {
            filters = getFiltersFromCache(filterCacheKey(filter), filter);
        },
        { lazy: true }
    );

    function onFilterChanged(addedOrUpdated: FacetedFilter.IFilter): void {
        if (addedOrUpdated.type !== 'date') {
            updateFilters(filterChanged(filters ?? [], addedOrUpdated));
        }

        selectedEventId = null;
    }

    function onFilterRemoved(removed?: FacetedFilter.IFilter): void {
        updateFilters(filterRemoved(filters ?? [], removed));
    }

    function updateFilters(updatedFilters: FacetedFilter.IFilter[]): void {
        const filter = toFilter(updatedFilters);
        updateFilterCache(filterCacheKey(filter), updatedFilters);
        params.filter = filter;
    }

    const context = getTableContext<EventSummaryModel<SummaryTemplateKeys>>({ limit: params.limit!, mode: 'summary' }, function (options) {
        options.columns = options.columns.filter((c) => c.id !== 'select').map((c) => ({ ...c, enableSorting: false }));
        options.enableMultiRowSelection = false;
        options.enableRowSelection = false;
        options.manualSorting = false;
        return options;
    });

    const table = createTable(context.options);

    const client = useFetchClient();
    const clientStatus = useFetchClientStatus(client);

    let response = $state<FetchClientResponse<EventSummaryModel<SummaryTemplateKeys>[]>>();
    let before: string | undefined;

    async function loadData(filterChanged: boolean = false) {
        if (!organization.current) {
            return;
        }

        if (client.isLoading && filterChanged && !before) {
            return;
        }

        if (filterChanged) {
            before = undefined;
        }

        response = await client.getJSON<EventSummaryModel<SummaryTemplateKeys>[]>(`organizations/${organization.current}/events`, {
            params: {
                ...context.parameters,
                before,
                filter: params.filter
            }
        });

        if (response.ok) {
            if (response.meta.links.previous?.before) {
                before = response.meta.links.previous?.before;
            }

            const data = filterChanged ? [] : context.data;
            for (const summary of response.data?.reverse() || []) {
                data.push(summary);
            }

            context.data = data.slice(-params.limit!);
            context.meta = response.meta;
        }
    }

    const debouncedLoadData = debounce(5000, loadData);
    async function onPersistentEventChanged(message: WebSocketMessageValue<'PersistentEventChanged'>) {
        if (message.id && message.change_type === ChangeType.Removed) {
            if (removeTableData(table, (doc) => doc.id === message.id)) {
                // If the grid data is empty from all events being removed, we should refresh the data.
                if (isTableEmpty(table)) {
                    await debouncedLoadData();
                    return;
                }
            }
        }

        // Do not refresh if the filter criteria doesn't match the web socket message.
        if (!shouldRefreshPersistentEventChanged(filters, params.filter, message.organization_id, message.project_id, message.stack_id, message.id)) {
            return;
        }

        await debouncedLoadData();
    }

    useEventListener(document, 'refresh', async () => await loadData());
    useEventListener(document, 'PersistentEventChanged', (event) => onPersistentEventChanged((event as CustomEvent).detail));

    $effect(() => {
        // Handle case where pop state loses the limit
        params.limit ??= DEFAULT_LIMIT;
    });

    $effect(() => {
        loadData();
    });
</script>

<Card.Root
    ><Card.Header>
        <Card.Title class="text-2xl" level={2}>Event Stream</Card.Title>
    </Card.Header>
    <Card.Content>
        <DataTable.Root>
            <DataTable.Toolbar {table}>
                <FacetedFilter.Root changed={onFilterChanged} {filters} remove={onFilterRemoved}>
                    <OrganizationDefaultsFacetedFilterBuilder includeDateFacets={false} />
                </FacetedFilter.Root>
            </DataTable.Toolbar>
            <DataTable.Body rowClick={rowclick} {table}>
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
                    <DataTable.PageSize bind:value={params.limit!} {table} />
                    <div class="text-center">
                        <ErrorMessage message={response?.problem?.errors.general} />
                    </div>
                </div>
            </DataTable.Footer>
        </DataTable.Root>
    </Card.Content></Card.Root
>

<Sheet.Root onOpenChange={() => (selectedEventId = null)} open={!!selectedEventId}>
    <Sheet.Content class="w-full overflow-y-auto sm:max-w-full md:w-5/6">
        <Sheet.Header>
            <Sheet.Title
                >Event Details <Button href="/next/event/{selectedEventId}" size="sm" title="Open in new window" variant="ghost"><ExternalLink /></Button
                ></Sheet.Title
            >
        </Sheet.Header>
        <EventsOverview filterChanged={onFilterChanged} id={selectedEventId || ''} handleError={() => (selectedEventId = null)}></EventsOverview>
    </Sheet.Content>
</Sheet.Root>
