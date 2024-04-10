<script lang="ts">
    import { derived, writable } from 'svelte/store';
    import { persisted } from 'svelte-persisted-store';
    import IconOpenInNew from '~icons/mdi/open-in-new';

    import { Button } from '$comp/ui/button';
    import * as Card from '$comp/ui/card';
    import * as Sheet from '$comp/ui/sheet';
    import * as FacetedFilter from '$comp/faceted-filter';

    import { getEventsByStackIdQuery } from '$api/eventsApi';
    import EventsDataTable from '$comp/events/table/EventsDataTable.svelte';
    import EventsDrawer from '$comp/events/EventsDrawer.svelte';
    import CustomEventMessage from '$comp/messaging/CustomEventMessage.svelte';
    import type { SummaryModel, SummaryTemplateKeys } from '$lib/models/api';

    import { type IFilter, FilterSerializer, toFilter, DateFilter, getDefaultFilters, filterChanged, filterRemoved } from '$comp/filters/filters';
    import { toFacetedFilters } from '$comp/filters/facets';

    const selectedStackId = writable<string | null>(null);
    function onRowClick({ detail }: CustomEvent<SummaryModel<SummaryTemplateKeys>>) {
        selectedStackId.set(detail.id);
    }

    // Load the latest event for the stack and display it in the sidebar.
    const eventsResponse = getEventsByStackIdQuery(selectedStackId, 1);
    const eventId = derived(eventsResponse, ($eventsResponse) => {
        return $eventsResponse?.data?.[0]?.id;
    });

    const limit = persisted<number>('events.issues.limit', 10);
    const defaultFilters = getDefaultFilters().filter((f) => f.key !== 'type');
    const filters = persisted<IFilter[]>('events.issues.filters', defaultFilters, { serializer: new FilterSerializer() });
    $filters.push(...defaultFilters.filter((df) => !$filters.some((f) => f.key === df.key)));

    const filter = derived(filters, ($filters) => toFilter($filters.filter((f) => f.key !== 'date:date')));
    const facets = derived(filters, ($filters) => toFacetedFilters($filters));
    const time = derived(filters, ($filters) => ($filters.find((f) => f.key === 'date:date') as DateFilter).value as string);

    function onDrawerFilterChanged({ detail }: CustomEvent<IFilter>): void {
        if (detail.key !== 'type') {
            filterChanged(filters, detail);
        }

        selectedStackId.set(null)
    }

    function onFilterChanged({ detail }: CustomEvent<IFilter>): void {
        if (detail.key !== 'type') {
            filterChanged(filters, detail);
        }
    }

    function onFilterRemoved({ detail }: CustomEvent<IFilter | undefined>): void {
        filterRemoved(filters, defaultFilters, detail);
    }
</script>

<CustomEventMessage type="filter" on:message={onDrawerFilterChanged}></CustomEventMessage>

<div class="flex flex-col space-y-4">
    <Card.Root>
        <Card.Title tag="h2" class="p-6 pb-4 text-2xl">Issues</Card.Title>
        <Card.Content>
            <EventsDataTable {filter} {limit} {time} on:rowclick={onRowClick} mode="stack_frequent" pageFilter="(type:404 OR type:error)">
                <svelte:fragment slot="toolbar">
                    <FacetedFilter.Root {facets} on:changed={onFilterChanged} on:remove={onFilterRemoved}></FacetedFilter.Root>
                </svelte:fragment>
            </EventsDataTable>
        </Card.Content>
    </Card.Root>
</div>

<Sheet.Root open={$eventsResponse.isSuccess} onOpenChange={() => selectedStackId.set(null)}>
    <Sheet.Content class="w-full overflow-y-auto sm:max-w-full md:w-5/6">
        <Sheet.Header>
            <Sheet.Title
                >Event Details <Button href="/event/{eventId}" variant="ghost" size="sm" title="Open in new window"><IconOpenInNew /></Button></Sheet.Title
            >
        </Sheet.Header>
        <EventsDrawer id={$eventId || ''}></EventsDrawer>
    </Sheet.Content>
</Sheet.Root>
