<script lang="ts">
    import { derived } from 'svelte/store';
    import { persisted } from 'svelte-persisted-store';
    import IconOpenInNew from '~icons/mdi/open-in-new';

    import { Button } from '$comp/ui/button';
    import * as Card from '$comp/ui/card';
    import * as Sheet from '$comp/ui/sheet';
    import * as FacetedFilter from '$comp/faceted-filter';

    import EventsDataTable from '$comp/events/table/EventsDataTable.svelte';
    import EventsDrawer from '$comp/events/EventsDrawer.svelte';
    import type { SummaryModel, SummaryTemplateKeys } from '$lib/models/api';

    import DateFacetedFilter from '$comp/filters/facets/DateFacetedFilter.svelte';
    import KeywordFacetedFilter from '$comp/filters/facets/KeywordFacetedFilter.svelte';
    import OrganizationFacetedFilter from '$comp/filters/facets/OrganizationFacetedFilter.svelte';
    import ProjectFacetedFilter from '$comp/filters/facets/ProjectFacetedFilter.svelte';
    import StatusFacetedFilter from '$comp/filters/facets/StatusFacetedFilter.svelte';
    import TypeFacetedFilter from '$comp/filters/facets/TypeFacetedFilter.svelte';
    import {
        StatusFilter,
        TypeFilter,
        type IFilter,
        FilterSerializer,
        KeywordFilter,
        toFilter,
        OrganizationFilter,
        ProjectFilter,
        DateFilter
    } from '$comp/filters/filters';

    let selectedEventId: string | null = null;
    function onRowClick({ detail }: CustomEvent<SummaryModel<SummaryTemplateKeys>>) {
        selectedEventId = detail.id;
    }

    const limit = persisted<number>('events.limit', 10);
    const defaultFilters = [
        new OrganizationFilter(''),
        new ProjectFilter('', []),
        new StatusFilter([]),
        new TypeFilter([]),
        new DateFilter('date', 'last week'),
        new KeywordFilter('')
    ];
    const filters = persisted<IFilter[]>('events.filters', defaultFilters, { serializer: new FilterSerializer() });
    $filters.push(...defaultFilters.filter((df) => !$filters.some((f) => f.type === df.type)));

    const filter = derived(filters, ($filters) => toFilter($filters.filter((f) => f.type !== 'date')));
    const facets = derived(filters, ($filters) => [
        {
            title: 'Organization',
            component: OrganizationFacetedFilter,
            filter: $filters.find((f) => f.type === 'organization')!
        },
        {
            title: 'Project',
            component: ProjectFacetedFilter,
            filter: $filters.find((f) => f.type === 'project')!
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
        },
        {
            title: 'Date Range',
            component: DateFacetedFilter,
            filter: $filters.find((f) => f.type === 'date')!
        },
        {
            title: 'Keyword',
            component: KeywordFacetedFilter,
            filter: $filters.find((f) => f.type === 'keyword')!
        }
    ]);
    const time = derived(filters, ($filters) => ($filters.find((f) => f.type === 'date') as DateFilter).value as string);

    function onFiltersChanged({ detail }: CustomEvent<IFilter[]>) {
        const organizationFilter = detail.find((f) => f.type === 'organization') as OrganizationFilter;
        const projectFilter = detail.find((f) => f.type === 'project') as ProjectFilter;
        if (organizationFilter.value !== projectFilter.organization) {
            projectFilter.organization = organizationFilter.value;
            projectFilter.value = [];
        }

        filters.set(detail);
    }
</script>

<div class="flex flex-col space-y-4">
    <Card.Root>
        <Card.Title tag="h2" class="p-6 pb-4 text-2xl">Events</Card.Title>
        <Card.Content>
            <EventsDataTable {filter} {limit} {time} on:rowclick={onRowClick}>
                <svelte:fragment slot="toolbar">
                    <FacetedFilter.Root {facets} on:changed={onFiltersChanged}></FacetedFilter.Root>
                </svelte:fragment>
            </EventsDataTable>
        </Card.Content>
    </Card.Root>
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
