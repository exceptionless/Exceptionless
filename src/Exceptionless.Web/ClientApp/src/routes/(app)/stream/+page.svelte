<script lang="ts">
    import { derived } from 'svelte/store';
    import { persisted } from 'svelte-persisted-store';
    import IconOpenInNew from '~icons/mdi/open-in-new';

    import { Button } from '$comp/ui/button';
    import * as Card from '$comp/ui/card';
    import * as Sheet from '$comp/ui/sheet';
    import * as FacetedFilter from '$comp/faceted-filter';

    import EventsTailLogDataTable from '$comp/events/table/EventsTailLogDataTable.svelte';
    import EventsDrawer from '$comp/events/EventsDrawer.svelte';
    import CustomEventMessage from '$comp/messaging/CustomEventMessage.svelte';
    import type { SummaryModel, SummaryTemplateKeys } from '$lib/models/api';

    import { type IFilter, FilterSerializer, toFilter, getDefaultFilters, filterChanged, filterRemoved } from '$comp/filters/filters';
    import { toFacetedFilters } from '$comp/filters/facets';

    let selectedEventId: string | null = null;
    function onRowClick({ detail }: CustomEvent<SummaryModel<SummaryTemplateKeys>>) {
        selectedEventId = detail.id;
    }

    const limit = persisted<number>('events.stream.limit', 10);
    const defaultFilters = getDefaultFilters(false);
    const filters = persisted<IFilter[]>('events.stream.filters', defaultFilters, { serializer: new FilterSerializer() });
    $filters.push(...defaultFilters.filter((df) => !$filters.some((f) => f.key === df.key)));

    const filter = derived(filters, ($filters) => toFilter($filters));
    const facets = derived(filters, ($filters) => toFacetedFilters($filters));

    function onFilterChanged({ detail }: CustomEvent<IFilter>): void {
        if (detail.key !== 'date:date') {
            filterChanged(filters, detail);
        }
    }

    function onFilterRemoved({ detail }: CustomEvent<IFilter | undefined>): void {
        filterRemoved(filters, defaultFilters, detail);
    }
</script>

<CustomEventMessage type="filter" on:message={onFilterChanged}></CustomEventMessage>

<Card.Root>
    <Card.Title tag="h2" class="p-6 pb-4 text-2xl">Event Stream</Card.Title>
    <Card.Content>
        <EventsTailLogDataTable {filter} {limit} on:rowclick={onRowClick}>
            <svelte:fragment slot="toolbar">
                <FacetedFilter.Root {facets} on:changed={onFilterChanged} on:remove={onFilterRemoved}></FacetedFilter.Root>
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
