<script lang="ts">
    import type { EventSummaryModel, SummaryTemplateKeys } from '$features/events/components/summary/index';

    import AutomaticRefreshIndicatorButton from '$comp/AutomaticRefreshIndicatorButton.svelte';
    import * as DataTable from '$comp/data-table';
    import * as FacetedFilter from '$comp/faceted-filter';
    import { toFacetedFilters } from '$comp/filters/facets';
    import { DateFilter, filterChanged, filterRemoved, FilterSerializer, getDefaultFilters, type IFilter, toFilter } from '$comp/filters/filters.svelte';
    import { Button } from '$comp/ui/button';
    import * as Card from '$comp/ui/card';
    import * as Sheet from '$comp/ui/sheet';
    import { getStackEventsQuery } from '$features/events/api.svelte';
    import EventsDrawer from '$features/events/components/EventsDrawer.svelte';
    import { shouldRefreshPersistentEventChanged } from '$features/events/components/filters';
    import EventsDataTable from '$features/events/components/table/EventsDataTable.svelte';
    import { getTableContext } from '$features/events/components/table/options.svelte';
    import TableStacksBulkActionsDropdownMenu from '$features/stacks/components/StacksBulkActionsDropdownMenu.svelte';
    import { ChangeType, type WebSocketMessageValue } from '$features/websockets/models';
    import { useFetchClientStatus } from '$shared/api/api.svelte';
    import { persisted } from '$shared/persisted.svelte';
    import { isTableEmpty, removeTableData, removeTableSelection } from '$shared/table';
    import { type FetchClientResponse, useFetchClient } from '@exceptionless/fetchclient';
    import { createTable } from '@tanstack/svelte-table';
    import { useEventListener } from 'runed';
    import { throttle } from 'throttle-debounce';
    import IconOpenInNew from '~icons/mdi/open-in-new';

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

    const limit = persisted<number>('events.issues.limit', 10);
    const defaultFilters = getDefaultFilters().filter((f) => f.key !== 'type');
    const persistedFilters = persisted<IFilter[]>('events.issues.filters', defaultFilters, new FilterSerializer());
    persistedFilters.value.push(...defaultFilters.filter((df) => !persistedFilters.value.some((f) => f.key === df.key)));

    const filter = $derived(toFilter(persistedFilters.value.filter((f) => f.key !== 'date:date')));
    const facets = $derived(toFacetedFilters(persistedFilters.value));
    const time = $derived<string>((persistedFilters.value.find((f) => f.key === 'date:date') as DateFilter).value as string);

    function onDrawerFilterChanged(filter: IFilter): void {
        if (filter.key !== 'type') {
            persistedFilters.value = filterChanged(persistedFilters.value, filter);
        }

        selectedStackId = undefined;
    }

    function onFilterChanged(filter: IFilter): void {
        if (filter.key !== 'type') {
            persistedFilters.value = filterChanged(persistedFilters.value, filter);
        }
    }

    function onFilterRemoved(filter?: IFilter): void {
        persistedFilters.value = filterRemoved(persistedFilters.value, defaultFilters, filter);
    }

    const context = getTableContext<EventSummaryModel<SummaryTemplateKeys>>({ limit: limit.value, mode: 'stack_frequent' });
    const table = createTable(context.options);
    const canRefresh = $derived(!table.getIsSomeRowsSelected() && !table.getIsAllRowsSelected() && !table.getCanPreviousPage());

    const client = useFetchClient();
    const clientStatus = useFetchClientStatus(client);
    let clientResponse = $state<FetchClientResponse<EventSummaryModel<SummaryTemplateKeys>[]>>();

    async function loadData() {
        if (client.isLoading) {
            return;
        }

        clientResponse = await client.getJSON<EventSummaryModel<SummaryTemplateKeys>[]>('events', {
            params: {
                ...context.parameters,
                filter: ['(type:404 OR type:error)', filter].filter(Boolean).join(' '),
                time
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
        if (!shouldRefreshPersistentEventChanged(persistedFilters.value, filter, message.organization_id, message.project_id, message.id)) {
            return;
        }

        // Do not refresh if the grid has selections or grid is currently paged.
        if (!canRefresh) {
            return;
        }

        await throttledLoadData();
    }

    useEventListener(document, 'refresh', async () => await loadData());
    useEventListener(document, 'StackChanged', async (event) => await onStackChanged((event as CustomEvent).detail));

    $effect(() => {
        loadData();
    });
</script>

<div class="flex flex-col space-y-4">
    <Card.Root>
        <Card.Title class="p-6 pb-0 text-2xl" level={2}>Issues <AutomaticRefreshIndicatorButton {canRefresh} refresh={loadData} /></Card.Title>
        <Card.Content class="pt-4">
            <EventsDataTable bind:limit={limit.value} isLoading={clientStatus.isLoading} rowClick={rowclick} {table}>
                {#snippet toolbarChildren()}
                    <FacetedFilter.Root changed={onFilterChanged} {facets} remove={onFilterRemoved}></FacetedFilter.Root>
                {/snippet}
                {#snippet footerChildren()}
                    <div class="h-9 min-w-[140px]">
                        {#if table.getSelectedRowModel().flatRows.length}
                            <TableStacksBulkActionsDropdownMenu {table} />
                        {/if}
                    </div>

                    <DataTable.PageSize bind:value={limit.value} {table}></DataTable.PageSize>
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
                >Event Details <Button href="/event/{eventId}" size="sm" title="Open in new window" variant="ghost"><IconOpenInNew /></Button></Sheet.Title
            >
        </Sheet.Header>
        <EventsDrawer changed={onDrawerFilterChanged} id={eventId || ''} close={() => (selectedStackId = undefined)}></EventsDrawer>
    </Sheet.Content>
</Sheet.Root>
