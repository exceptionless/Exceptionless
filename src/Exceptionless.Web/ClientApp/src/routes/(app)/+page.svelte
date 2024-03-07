<script lang="ts">
    import { derived } from 'svelte/store';
    import { persisted } from 'svelte-persisted-store';
    import * as Card from '$comp/ui/card';
    import * as Sheet from '$comp/ui/sheet';
    import * as FacetedFilter from '$comp/facets';
    import SearchInput from '$comp/SearchInput.svelte';
    import PieChartCard from '$comp/events/cards/pie-chart-card.svelte';

    import EventsDataTable from '$comp/events/table/EventsDataTable.svelte';
    import EventsDrawer from '$comp/events/EventsDrawer.svelte';
    import IconOpenInNew from '~icons/mdi/open-in-new';
    import type { SummaryModel, SummaryTemplateKeys } from '$lib/models/api';
    import CustomEventMessage from '$comp/messaging/CustomEventMessage.svelte';
    import { filter, filterWithFaceted, onFilterChanged, onFilterInputChanged, time } from '$lib/stores/events';
    import DateRangeDropdown from '$comp/DateRangeDropdown.svelte';
    import { Button } from '$comp/ui/button';
    import StatusFacetedFilter from '$comp/events/facets/StatusFacetedFilter.svelte';
    import TypeFacetedFilter from '$comp/events/facets/TypeFacetedFilter.svelte';
    import { StatusFilter, TypeFilter, type IFilter, FilterSerializer } from '$comp/filters/filters';

    let selectedEventId: string | null = null;
    function onRowClick({ detail }: CustomEvent<SummaryModel<SummaryTemplateKeys>>) {
        selectedEventId = detail.id;
    }

    const defaultFilters = [new StatusFilter([]), new TypeFilter([])];
    const filters = persisted<IFilter[]>('events.filters', defaultFilters, { serializer: new FilterSerializer() });
    $filters.push(...defaultFilters.filter((df) => !$filters.some((f) => f.type === df.type)));

    const facets = derived(filters, ($filters) => [
        {
            title: 'Status',
            component: StatusFacetedFilter,
            filter: $filters.find((f) => f.type === 'status')!
        },
        {
            title: 'Type',
            component: TypeFacetedFilter,
            filter: $filters.find((f) => f.type === 'type')!
        }
    ]);

    function onFiltersChanged({ detail }: CustomEvent<IFilter[]>) {
        filters.set(detail);
    }
</script>

<CustomEventMessage type="filter" on:message={onFilterChanged}></CustomEventMessage>
<div class="flex flex-col space-y-4">
    <Card.Root>
        <Card.Title tag="h2" class="p-6 pb-4 text-2xl">Events</Card.Title>
        <Card.Content>
            <EventsDataTable filter={filterWithFaceted} {time} on:rowclick={onRowClick}>
                <svelte:fragment slot="toolbar">
                    <SearchInput class="h-8 w-80 lg:w-[350px] xl:w-[550px]" value={$filter} on:input={onFilterInputChanged} />
                    <FacetedFilter.Root {facets} on:changed={onFiltersChanged}></FacetedFilter.Root>
                    <DateRangeDropdown bind:value={$time}></DateRangeDropdown>
                </svelte:fragment>
            </EventsDataTable>
        </Card.Content>
    </Card.Root>

    <PieChartCard title="Status"></PieChartCard>
</div>

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
