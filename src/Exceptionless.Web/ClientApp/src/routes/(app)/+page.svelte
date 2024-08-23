<script lang="ts">
    import type { EventSummaryModel, SummaryTemplateKeys } from '$features/events/summary/index';

    import * as FacetedFilter from '$comp/faceted-filter';
    import { toFacetedFilters } from '$comp/filters/facets';
    import { DateFilter, filterChanged, filterRemoved, FilterSerializer, getDefaultFilters, type IFilter, toFilter } from '$comp/filters/filters.svelte';
    import { Button } from '$comp/ui/button';
    import * as Card from '$comp/ui/card';
    import * as Sheet from '$comp/ui/sheet';
    import EventsDrawer from '$features/events/components/EventsDrawer.svelte';
    import EventsDataTable from '$features/events/components/table/EventsDataTable.svelte';
    import { persisted } from '$shared/persisted.svelte';
    import IconOpenInNew from '~icons/mdi/open-in-new';

    let selectedEventId: null | string = $state(null);
    function rowclick(row: EventSummaryModel<SummaryTemplateKeys>) {
        selectedEventId = row.id;
    }

    const limit = persisted<number>('events.limit', 10);
    const defaultFilters = getDefaultFilters();
    const persistedFilters = persisted<IFilter[]>('events.filters', defaultFilters, new FilterSerializer());
    persistedFilters.value.push(...defaultFilters.filter((df) => !persistedFilters.value.some((f) => f.key === df.key)));

    const filter = $derived(toFilter(persistedFilters.value.filter((f) => f.key !== 'date:date')));
    const facets = $derived(toFacetedFilters(persistedFilters.value));
    const time = $derived<string>((persistedFilters.value.find((f) => f.key === 'date:date') as DateFilter).value as string);

    function onDrawerFilterChanged(filter: IFilter): void {
        persistedFilters.value = filterChanged(persistedFilters.value, filter);
        selectedEventId = null;
    }

    function onFilterChanged(filter: IFilter): void {
        persistedFilters.value = filterChanged(persistedFilters.value, filter);
    }

    function onFilterRemoved(filter?: IFilter): void {
        persistedFilters.value = filterRemoved(persistedFilters.value, defaultFilters, filter);
    }
</script>

<div class="flex flex-col space-y-4">
    <Card.Root>
        <Card.Title class="p-6 pb-4 text-2xl" tag="h2">Events</Card.Title>
        <Card.Content>
            <EventsDataTable bind:limit={limit.value} {filter} {rowclick} {time}>
                {#snippet toolbarChildren()}
                    <FacetedFilter.Root changed={onFilterChanged} {facets} remove={onFilterRemoved}></FacetedFilter.Root>
                {/snippet}
            </EventsDataTable>
        </Card.Content>
    </Card.Root>
</div>

<Sheet.Root onOpenChange={() => (selectedEventId = null)} open={!!selectedEventId}>
    <Sheet.Content class="w-full overflow-y-auto sm:max-w-full md:w-5/6">
        <Sheet.Header>
            <Sheet.Title
                >Event Details <Button href="/event/{selectedEventId}" size="sm" title="Open in new window" variant="ghost"><IconOpenInNew /></Button
                ></Sheet.Title
            >
        </Sheet.Header>
        <EventsDrawer changed={onDrawerFilterChanged} id={selectedEventId || ''}></EventsDrawer>
    </Sheet.Content>
</Sheet.Root>
