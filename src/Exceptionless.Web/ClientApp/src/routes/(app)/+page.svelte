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
        setFilter,
        ReferenceFilter,
        SessionFilter
    } from '$comp/filters/filters';
    import CustomEventMessage from '$comp/messaging/CustomEventMessage.svelte';
    import { toFacetedFilters } from '$comp/filters/facets';

    let selectedEventId: string | null = null;
    function onRowClick({ detail }: CustomEvent<SummaryModel<SummaryTemplateKeys>>) {
        selectedEventId = detail.id;
    }

    const limit = persisted<number>('events.limit', 10);
    const defaultFilters = [
        new OrganizationFilter(),
        new ProjectFilter(undefined, []),
        new StatusFilter([]),
        new TypeFilter([]),
        new DateFilter('date', 'last week'),
        new ReferenceFilter(),
        new SessionFilter(),
        new KeywordFilter()
    ];
    const filters = persisted<IFilter[]>('events.filters', defaultFilters, { serializer: new FilterSerializer() });
    $filters.push(...defaultFilters.filter((df) => !$filters.some((f) => f.key === df.key)));

    const filter = derived(filters, ($filters) => toFilter($filters.filter((f) => f.key !== 'date:date')));
    const facets = derived(filters, ($filters) => toFacetedFilters($filters));
    const time = derived(filters, ($filters) => ($filters.find((f) => f.key === 'date:date') as DateFilter).value as string);

    function onFilterChanged({ detail }: CustomEvent<IFilter>): void {
        filters.set(processFilterRules(setFilter($filters, detail), detail));
    }

    function onFilterRemoved({ detail }: CustomEvent<IFilter | undefined>): void {
        // If detail is undefined, remove all filters.
        if (!detail) {
            filters.set(defaultFilters);
        } else if (defaultFilters.find((f) => f.key === detail.key)) {
            filters.set(processFilterRules(setFilter($filters, detail)));
        } else {
            filters.set(processFilterRules($filters.filter((f) => f.key !== detail.key)));
        }
    }

    function processFilterRules(filters: IFilter[], changed?: IFilter): IFilter[] {
        // Allow only one filter per type and term.
        const groupedFilters: Record<string, IFilter[]> = Object.groupBy(filters, (f: IFilter) => f.key);
        const filtered: IFilter[] = [];
        Object.entries(groupedFilters).forEach(([group, items]) => {
            filtered.push(items[0]);
        });

        const projectFilter = filtered.find((f) => f.type === 'project') as ProjectFilter;
        if (projectFilter) {
            let organizationFilter = filtered.find((f) => f.type === 'organization') as OrganizationFilter;

            // If there is a project filter, verify the organization filter is set
            if (!organizationFilter) {
                organizationFilter = new OrganizationFilter(projectFilter.organization);
                filtered.push(organizationFilter);
            }

            // If the organization filter changes and organization is not set on the project filter, clear the project filter
            if (changed?.type === 'organization' && projectFilter.organization !== organizationFilter.value) {
                projectFilter.organization = organizationFilter.value;
                projectFilter.value = [];
            }

            // If the project filter changes and the organization filter is not set, set it
            if (organizationFilter.value !== projectFilter.organization) {
                organizationFilter.value = projectFilter.organization;
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
                    <FacetedFilter.Root {facets} on:changed={onFilterChanged} on:remove={onFilterRemoved}></FacetedFilter.Root>
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
