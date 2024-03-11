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
    import type { SummaryModel, SummaryTemplateKeys } from '$lib/models/api';

    import KeywordFacetedFilter from '$comp/filters/facets/KeywordFacetedFilter.svelte';
    import OrganizationFacetedFilter from '$comp/filters/facets/OrganizationFacetedFilter.svelte';
    import StatusFacetedFilter from '$comp/filters/facets/StatusFacetedFilter.svelte';
    import TypeFacetedFilter from '$comp/filters/facets/TypeFacetedFilter.svelte';
    import { StatusFilter, TypeFilter, type IFilter, FilterSerializer, KeywordFilter, toFilter, OrganizationFilter } from '$comp/filters/filters';

    let selectedEventId: string | null = null;
    function onRowClick({ detail }: CustomEvent<SummaryModel<SummaryTemplateKeys>>) {
        selectedEventId = detail.id;
    }

    const limit = persisted<number>('events.stream.limit', 10);
    const defaultFilters = [new KeywordFilter(''), new OrganizationFilter(''), new StatusFilter([]), new TypeFilter([])];
    const filters = persisted<IFilter[]>('events.stream.filters', defaultFilters, { serializer: new FilterSerializer() });
    $filters.push(...defaultFilters.filter((df) => !$filters.some((f) => f.type === df.type)));

    const filter = derived(filters, ($filters) => toFilter($filters));
    const facets = derived(filters, ($filters) => [
        {
            title: 'Search',
            component: KeywordFacetedFilter,
            filter: $filters.find((f) => f.type === 'keyword')!
        },
        {
            title: 'Organization',
            component: OrganizationFacetedFilter,
            filter: $filters.find((f) => f.type === 'organization')!
        },
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

<Card.Root>
    <Card.Title tag="h2" class="p-6 pb-4 text-2xl">Event Stream</Card.Title>
    <Card.Content>
        <EventsTailLogDataTable {filter} {limit} on:rowclick={onRowClick}>
            <svelte:fragment slot="toolbar">
                <FacetedFilter.Root {facets} on:changed={onFiltersChanged}></FacetedFilter.Root>
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
