<script lang="ts">
    import type { EventSummaryModel, SummaryTemplateKeys } from '$features/events/components/summary/index';

    import * as DataTable from '$comp/data-table';
    import DelayedRender from '$comp/delayed-render.svelte';
    import ErrorMessage from '$comp/error-message.svelte';
    import * as FacetedFilter from '$comp/faceted-filter';
    import { Button } from '$comp/ui/button';
    import * as Card from '$comp/ui/card';
    import * as Sheet from '$comp/ui/sheet';
    import EventsDrawer from '$features/events/components/events-drawer.svelte';
    import {
        clearFilterCache,
        filterChanged,
        filterRemoved,
        getFiltersFromCache,
        shouldRefreshPersistentEventChanged,
        toFilter,
        updateFilterCache
    } from '$features/events/components/filters/helpers';
    import OrganizationDefaultsFacetedFilterBuilder from '$features/events/components/filters/organization-defaults-faceted-filter-builder.svelte';
    import { getTableContext } from '$features/events/components/table/options.svelte';
    import { organization } from '$features/organizations/context.svelte';
    import { ChangeType, type WebSocketMessageValue } from '$features/websockets/models';
    import { DEFAULT_LIMIT, useFetchClientStatus } from '$shared/api/api.svelte';
    import { isTableEmpty, removeTableData } from '$shared/table';
    import { type FetchClientResponse, useFetchClient } from '@exceptionless/fetchclient';
    import { createTable } from '@tanstack/svelte-table';
    import { queryParamsState } from 'kit-query-params';
    import ExternalLink from 'lucide-svelte/icons/external-link';
    import { useEventListener } from 'runed';
    import { debounce } from 'throttle-debounce';

    let selectedEventId: null | string = $state(null);
    function rowclick(row: EventSummaryModel<SummaryTemplateKeys>) {
        selectedEventId = row.id;
    }

    const params = queryParamsState({
        default: {
            filter: '',
            limit: DEFAULT_LIMIT
        },
        pushHistory: true,
        schema: {
            filter: 'string',
            limit: 'number'
        }
    });

    function onSwitchOrganization() {
        clearFilterCache();
        //params.$reset(); // Work around for https://github.com/beynar/kit-query-params/issues/7
        params.filter = '';
        params.limit = DEFAULT_LIMIT;
    }

    let filters = $state(getFiltersFromCache(params.filter));
    $effect(() => {
        // Handle case where pop state loses the limit
        params.limit ??= DEFAULT_LIMIT;

        // Track filter changes when the parameters change
        filters = getFiltersFromCache(params.filter);
    });

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
        updateFilterCache(filter, updatedFilters);
        filters = updatedFilters;
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
    useEventListener(document, 'switch-organization', onSwitchOrganization);
    useEventListener(document, 'PersistentEventChanged', (event) => onPersistentEventChanged((event as CustomEvent).detail));

    $effect(() => {
        loadData();
    });
</script>

<Card.Root>
    <Card.Title class="p-6 pb-0 text-2xl" level={2}>Event Stream</Card.Title>
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
                >Event Details <Button href="/event/{selectedEventId}" size="sm" title="Open in new window" variant="ghost"><ExternalLink /></Button
                ></Sheet.Title
            >
        </Sheet.Header>
        <EventsDrawer changed={onFilterChanged} id={selectedEventId || ''} close={() => (selectedEventId = null)}></EventsDrawer>
    </Sheet.Content>
</Sheet.Root>
