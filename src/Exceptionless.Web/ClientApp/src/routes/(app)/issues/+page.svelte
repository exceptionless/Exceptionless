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
    import type { SummaryModel, SummaryTemplateKeys } from '$lib/models/api';
    import DateRangeDropdown from '$comp/DateRangeDropdown.svelte';

    import KeywordFacetedFilter from '$comp/filters/facets/KeywordFacetedFilter.svelte';
    import OrganizationFacetedFilter from '$comp/filters/facets/OrganizationFacetedFilter.svelte';
    import StatusFacetedFilter from '$comp/filters/facets/StatusFacetedFilter.svelte';
    import TypeFacetedFilter from '$comp/filters/facets/TypeFacetedFilter.svelte';
    import { StatusFilter, TypeFilter, type IFilter, FilterSerializer, KeywordFilter, toFilter, OrganizationFilter } from '$comp/filters/filters';

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
    const time = persisted<string>('events.issues.time', '');
    const defaultFilters = [new KeywordFilter(''), new OrganizationFilter(''), new StatusFilter([]), new TypeFilter([])];
    const filters = persisted<IFilter[]>('events.issues.filters', defaultFilters, { serializer: new FilterSerializer() });
    $filters.push(...defaultFilters.filter((df) => !$filters.some((f) => f.type === df.type)));

    const filter = derived(filters, ($filters) => toFilter($filters));
    const facets = derived(filters, ($filters) => [
        {
            title: 'Keyword',
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

<div class="flex flex-col space-y-4">
    <Card.Root>
        <Card.Title tag="h2" class="p-6 pb-4 text-2xl">Issues</Card.Title>
        <Card.Content>
            <EventsDataTable {filter} {limit} {time} on:rowclick={onRowClick} mode="stack_frequent" pageFilter="(type:404 OR type:error)">
                <svelte:fragment slot="toolbar">
                    <FacetedFilter.Root {facets} on:changed={onFiltersChanged}></FacetedFilter.Root>
                    <DateRangeDropdown bind:value={$time}></DateRangeDropdown>
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
