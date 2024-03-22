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
        DateFilter,
        setFilter
    } from '$comp/filters/filters';
    import CustomEventMessage from '$comp/messaging/CustomEventMessage.svelte';
    import { toFacetedFilters } from '$comp/filters/facets';

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
    const facets = derived(filters, ($filters) => toFacetedFilters($filters));
    const time = derived(filters, ($filters) => ($filters.find((f) => f.type === 'date') as DateFilter).value as string);

    function onFiltersChanged({ detail }: CustomEvent<IFilter[]>) {
        filters.set(processFilterRules(detail));
    }

    function onFilterChanged({ detail }: CustomEvent<IFilter>): void {
        console.log('Filter changed', detail);
        filters.set(processFilterRules(setFilter($filters, detail)));
    }

    function processFilterRules(filters: IFilter[]): IFilter[] {
        console.log('Running rules');
        // Allow only one filter per type and term.
        const groupedFilters: Record<string, IFilter[]> = Object.groupBy(filters, (f: IFilter) => `${f.type}:{${'term' in f ? f.term : ''}`);
        const filtered: IFilter[] = [];
        Object.entries(groupedFilters).forEach(([group, items]) => {
            console.log('processing group', group, items[0]);
            filtered.push(items[0]);
        });

        const projectFilter = filtered.find((f) => f.type === 'project') as ProjectFilter;
        if (projectFilter) {
            const organizationFilter = filtered.find((f) => f.type === 'organization') as OrganizationFilter;

            // If there is a project filter, verify the organization filter is set
            if (organizationFilter) {
                if (organizationFilter.value !== projectFilter.organization) {
                    organizationFilter.value = projectFilter.organization;
                }
            } else {
                filtered.push(new OrganizationFilter(projectFilter.organization));
            }
        }

        return filtered;
    }
</script>

<CustomEventMessage type="filter" on:message={onFilterChanged}></CustomEventMessage>

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
