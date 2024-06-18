<script lang="ts">
    import IconOpenInNew from '~icons/mdi/open-in-new';

    import { Button } from '$comp/ui/button';
    import * as Card from '$comp/ui/card';
    import * as Sheet from '$comp/ui/sheet';
    import * as FacetedFilter from '$comp/faceted-filter';

    import EventsTailLogDataTable from '$comp/events/table/EventsTailLogDataTable.svelte';
    import EventsDrawer from '$comp/events/EventsDrawer.svelte';
    import type { SummaryModel, SummaryTemplateKeys } from '$lib/models/api';

    import { type IFilter, FilterSerializer, toFilter, getDefaultFilters, filterChanged, filterRemoved } from '$comp/filters/filters';
    import { toFacetedFilters } from '$comp/filters/facets';
    import { persisted } from '$lib/helpers/persisted.svelte';

    let selectedEventId: string | null = $state(null);
    function onRowClick({ detail }: CustomEvent<SummaryModel<SummaryTemplateKeys>>) {
        selectedEventId = detail.id;
    }

    const limit = persisted<number>('events.stream.limit', 10);
    const defaultFilters = getDefaultFilters(false);
    const persistedFilters = persisted<IFilter[]>('events.stream.filters', defaultFilters, new FilterSerializer());
    persistedFilters.value.push(...defaultFilters.filter((df) => !persistedFilters.value.some((f) => f.key === df.key)));

    const filter = $derived(toFilter(persistedFilters.value));
    const facets = $derived(toFacetedFilters(persistedFilters.value));

    function onDrawerFilterChanged({ detail }: CustomEvent<IFilter>): void {
        filterChanged(persistedFilters.value, detail);
        selectedEventId = null;
    }

    function onFilterChanged(filter: IFilter): void {
        if (filter.key !== 'date:date') {
            filterChanged(persistedFilters.value, filter);
        }
    }

    function onFilterRemoved(filter?: IFilter): void {
        filterRemoved(persistedFilters.value, defaultFilters, filter);
    }
</script>

<Card.Root>
    <Card.Title tag="h2" class="p-6 pb-4 text-2xl">Event Stream</Card.Title>
    <Card.Content>
        <EventsTailLogDataTable {filter} limit={limit.value} on:rowclick={onRowClick}>
            <svelte:fragment slot="toolbar">
                <FacetedFilter.Root {facets} changed={onFilterChanged} remove={onFilterRemoved}></FacetedFilter.Root>
            </svelte:fragment>
        </EventsTailLogDataTable>
    </Card.Content></Card.Root
>

<Sheet.Root open={!!selectedEventId} onOpenChange={() => (selectedEventId = null)}>
    <Sheet.Content class="w-full overflow-y-auto sm:max-w-full md:w-5/6">
        <Sheet.Header>
            <Sheet.Title
                >Event Details <Button href="/event/{selectedEventId}" variant="ghost" size="sm" title="Open in new window"><IconOpenInNew /></Button
                ></Sheet.Title
            >
        </Sheet.Header>
        <EventsDrawer id={selectedEventId || ''}></EventsDrawer>
    </Sheet.Content>
</Sheet.Root>
