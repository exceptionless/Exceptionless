<script lang="ts">
    import type { EventSummaryModel, SummaryTemplateKeys } from '$features/events/summary/index';

    import * as FacetedFilter from '$comp/faceted-filter';
    import { toFacetedFilters } from '$comp/filters/facets';
    import { DateFilter, filterChanged, filterRemoved, FilterSerializer, getDefaultFilters, type IFilter, toFilter } from '$comp/filters/filters.svelte';
    import { Button } from '$comp/ui/button';
    import * as Card from '$comp/ui/card';
    import * as Sheet from '$comp/ui/sheet';
    import { getEventsByStackIdQuery } from '$features/events/api.svelte';
    import EventsDrawer from '$features/events/components/EventsDrawer.svelte';
    import EventsDataTable from '$features/events/components/table/EventsDataTable.svelte';
    import { persisted } from '$shared/persisted.svelte';
    import IconOpenInNew from '~icons/mdi/open-in-new';

    let selectedStackId = $state<string>();
    function rowclick(row: EventSummaryModel<SummaryTemplateKeys>) {
        selectedStackId = row.id;
    }

    // Load the latest event for the stack and display it in the sidebar.
    const eventsResponse = getEventsByStackIdQuery({
        limit: 1,
        get stackId() {
            return selectedStackId;
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
</script>

<div class="flex flex-col space-y-4">
    <Card.Root>
        <Card.Title class="p-6 pb-4 text-2xl" tag="h2">Issues</Card.Title>
        <Card.Content>
            <EventsDataTable bind:limit={limit.value} {filter} mode="stack_frequent" pageFilter="(type:404 OR type:error)" {rowclick} {time}>
                {#snippet toolbarChildren()}
                    <FacetedFilter.Root changed={onFilterChanged} {facets} remove={onFilterRemoved}></FacetedFilter.Root>
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
        <EventsDrawer changed={onDrawerFilterChanged} id={eventId || ''}></EventsDrawer>
    </Sheet.Content>
</Sheet.Root>
