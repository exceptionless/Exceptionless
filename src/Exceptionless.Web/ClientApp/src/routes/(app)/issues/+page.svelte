<script lang="ts">
    import type { EventSummaryModel, SummaryTemplateKeys } from '$features/events/components/summary/index';

    import AutomaticRefreshIndicatorButton from '$comp/automatic-refresh-indicator-button.svelte';
    import * as DataTable from '$comp/data-table';
    import * as FacetedFilter from '$comp/faceted-filter';
    import { Button } from '$comp/ui/button';
    import * as Card from '$comp/ui/card';
    import * as Sheet from '$comp/ui/sheet';
    import { getStackEventsQuery } from '$features/events/api.svelte';
    import EventsDrawer from '$features/events/components/events-drawer.svelte';
    import { type DateFilter, StatusFilter, TypeFilter } from '$features/events/components/filters';
    import {
        applyDefaultDateFilter,
        clearFilterCache,
        filterChanged,
        filterRemoved,
        getFiltersFromCache,
        shouldRefreshPersistentEventChanged,
        toFilter,
        updateFilterCache
    } from '$features/events/components/filters/helpers';
    import OrganizationDefaultsFacetedFilterBuilder from '$features/events/components/filters/organization-defaults-faceted-filter-builder.svelte';
    import EventsDataTable from '$features/events/components/table/events-data-table.svelte';
    import { getTableContext } from '$features/events/components/table/options.svelte';
    import { organization } from '$features/organizations/context.svelte';
    import TableStacksBulkActionsDropdownMenu from '$features/stacks/components/stacks-bulk-actions-dropdown-menu.svelte';
    import { StackStatus } from '$features/stacks/models';
    import { ChangeType, type WebSocketMessageValue } from '$features/websockets/models';
    import { DEFAULT_LIMIT, useFetchClientStatus } from '$shared/api/api.svelte';
    import { isTableEmpty, removeTableData, removeTableSelection } from '$shared/table';
    import { type FetchClientResponse, useFetchClient } from '@exceptionless/fetchclient';
    import { createTable } from '@tanstack/svelte-table';
    import { queryParamsState } from 'kit-query-params';
    import ExternalLink from 'lucide-svelte/icons/external-link';
    import { useEventListener } from 'runed';
    import { throttle } from 'throttle-debounce';

    // TODO: Update this page to use StackSummaryModel instead of EventSummaryModel.
    let selectedStackId = $state<string>();
    function rowclick(row: EventSummaryModel<SummaryTemplateKeys>) {
        selectedStackId = row.id;
    }

    // Load the latest event for the stack and display it in the sidebar.
    const eventsResponse = getStackEventsQuery({
        params: {
            limit: 1
        },
        route: {
            get stackId() {
                return selectedStackId;
            }
        }
    });
    const eventId = $derived(eventsResponse?.data?.[0]?.id);

    const DEFAULT_FILTERS = [new TypeFilter(['404', 'error']), new StatusFilter([StackStatus.Open, StackStatus.Regressed])];
    const DEFAULT_PARAMS = {
        filter: '(type:404 OR type:error) (status:open OR status:regressed)',
        limit: DEFAULT_LIMIT,
        time: 'last week'
    };

    updateFilterCache(DEFAULT_PARAMS.filter, DEFAULT_FILTERS);
    const params = queryParamsState({
        default: DEFAULT_PARAMS,
        pushHistory: true,
        schema: {
            filter: 'string',
            limit: 'number',
            time: 'string'
        }
    });

    function onSwitchOrganization() {
        clearFilterCache();
        updateFilterCache(DEFAULT_PARAMS.filter, DEFAULT_FILTERS);
        //params.$reset(); // Work around for https://github.com/beynar/kit-query-params/issues/7
        Object.assign(params, DEFAULT_PARAMS);
    }

    let filters = $state(applyDefaultDateFilter(getFiltersFromCache(params.filter), params.time));
    $effect(() => {
        // Handle case where pop state loses the limit
        params.limit ??= DEFAULT_LIMIT;

        // Track filter changes when the parameters change
        filters = applyDefaultDateFilter(getFiltersFromCache(params.filter), params.time);
    });

    function onFilterChanged(addedOrUpdated: FacetedFilter.IFilter): void {
        if (addedOrUpdated.type !== 'type') {
            updateFilters(filterChanged(filters ?? [], addedOrUpdated));
        }

        selectedStackId = undefined;
    }

    function onFilterRemoved(removed?: FacetedFilter.IFilter): void {
        updateFilters(filterRemoved(filters ?? [], removed));
    }

    function updateFilters(updatedFilters: FacetedFilter.IFilter[]): void {
        const filter = toFilter(updatedFilters.filter((f) => f.type !== 'date'));

        updateFilterCache(filter, updatedFilters);
        filters = updatedFilters;
        params.time = (updatedFilters.find((f) => f.type === 'date') as DateFilter)?.value as string;
        params.filter = filter;
    }

    const context = getTableContext<EventSummaryModel<SummaryTemplateKeys>>({ limit: params.limit!, mode: 'stack_frequent' });
    const table = createTable(context.options);
    const canRefresh = $derived(!table.getIsSomeRowsSelected() && !table.getIsAllRowsSelected() && !table.getCanPreviousPage());

    const client = useFetchClient();
    const clientStatus = useFetchClientStatus(client);
    let clientResponse = $state<FetchClientResponse<EventSummaryModel<SummaryTemplateKeys>[]>>();

    async function loadData() {
        if (client.isLoading || !organization.current) {
            return;
        }

        clientResponse = await client.getJSON<EventSummaryModel<SummaryTemplateKeys>[]>(`organizations/${organization.current}/events`, {
            params: {
                ...context.parameters,
                filter: params.filter,
                time: params.time
            }
        });

        if (clientResponse.ok) {
            context.data = clientResponse.data || [];
            context.meta = clientResponse.meta;
        }
    }
    const throttledLoadData = throttle(5000, loadData);

    async function onStackChanged(message: WebSocketMessageValue<'StackChanged'>) {
        if (message.id && message.change_type === ChangeType.Removed) {
            removeTableSelection(table, message.id);

            if (removeTableData(table, (doc) => doc.id === message.id)) {
                // If the grid data is empty from all events being removed, we should refresh the data.
                if (isTableEmpty(table)) {
                    await throttledLoadData();
                    return;
                }
            }
        }

        // Do not refresh if the filter criteria doesn't match the web socket message.
        if (!shouldRefreshPersistentEventChanged(filters, params.filter, message.organization_id, message.project_id, message.id)) {
            return;
        }

        // Do not refresh if the grid has selections or grid is currently paged.
        if (!canRefresh) {
            return;
        }

        await throttledLoadData();
    }

    useEventListener(document, 'refresh', async () => await loadData());
    useEventListener(document, 'switch-organization', onSwitchOrganization);
    useEventListener(document, 'StackChanged', async (event) => await onStackChanged((event as CustomEvent).detail));

    $effect(() => {
        loadData();
    });
</script>

<div class="flex flex-col space-y-4">
    <Card.Root>
        <Card.Title class="p-6 pb-0 text-2xl" level={2}>Issues <AutomaticRefreshIndicatorButton {canRefresh} refresh={loadData} /></Card.Title>
        <Card.Content class="pt-4">
            <EventsDataTable bind:limit={params.limit!} isLoading={clientStatus.isLoading} rowClick={rowclick} {table}>
                {#snippet toolbarChildren()}
                    <FacetedFilter.Root changed={onFilterChanged} {filters} remove={onFilterRemoved}>
                        <OrganizationDefaultsFacetedFilterBuilder />
                    </FacetedFilter.Root>
                {/snippet}
                {#snippet footerChildren()}
                    <div class="h-9 min-w-[140px]">
                        {#if table.getSelectedRowModel().flatRows.length}
                            <TableStacksBulkActionsDropdownMenu {table} />
                        {/if}
                    </div>

                    <DataTable.PageSize bind:value={params.limit!} {table}></DataTable.PageSize>
                    <div class="flex items-center space-x-6 lg:space-x-8">
                        <DataTable.PageCount {table} />
                        <DataTable.Pagination {table} />
                    </div>
                {/snippet}
            </EventsDataTable>
        </Card.Content>
    </Card.Root>
</div>

<Sheet.Root onOpenChange={() => (selectedStackId = undefined)} open={eventsResponse.isSuccess}>
    <Sheet.Content class="w-full overflow-y-auto sm:max-w-full md:w-5/6">
        <Sheet.Header>
            <Sheet.Title
                >Event Details <Button href="/event/{eventId}" size="sm" title="Open in new window" variant="ghost"><ExternalLink /></Button></Sheet.Title
            >
        </Sheet.Header>
        <EventsDrawer changed={onFilterChanged} id={eventId || ''} close={() => (selectedStackId = undefined)}></EventsDrawer>
    </Sheet.Content>
</Sheet.Root>
