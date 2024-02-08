<script lang="ts">
    import * as Card from '$comp/ui/card';
    import * as Sheet from '$comp/ui/sheet';
    import SearchInput from '$comp/SearchInput.svelte';
    import { getEventsByStackIdQuery } from '$api/queries/events';

    import EventsDataTable from '$comp/events/table/EventsDataTable.svelte';
    import EventsDrawer from '$comp/events/EventsDrawer.svelte';
    import type { SummaryModel, SummaryTemplateKeys } from '$lib/models/api';
    import CustomEventMessage from '$comp/messaging/CustomEventMessage.svelte';
    import { filter, filterWithFaceted, onFilterChanged, onFilterInputChanged, time } from '$lib/stores/events';
    import DateRangeDropdown from '$comp/DateRangeDropdown.svelte';
    import { writable } from 'svelte/store';

    const selectedStackId = writable<string | null>(null);
    const eventsResponse = getEventsByStackIdQuery(selectedStackId, 1);
    function onRowClick({ detail }: CustomEvent<SummaryModel<SummaryTemplateKeys>>) {
        // TODO: We need to load the latest event for the stack and display it in the sidebar.
        selectedStackId.set(detail.id);
    }
</script>

<CustomEventMessage type="filter" on:message={onFilterChanged}></CustomEventMessage>

<div class="flex flex-col space-y-4">
    <Card.Root>
        <Card.Title tag="h2" class="p-6 pb-4 text-2xl">Issues</Card.Title>
        <Card.Content>
            <EventsDataTable filter={filterWithFaceted} {time} on:rowclick={onRowClick} mode="stack_frequent" pageFilter="(type:404 OR type:error)">
                <svelte:fragment slot="toolbar">
                    <SearchInput class="h-8 w-80 lg:w-[350px] xl:w-[550px]" value={$filter} on:input={onFilterInputChanged} />

                    <DateRangeDropdown bind:value={$time}></DateRangeDropdown>
                </svelte:fragment>
            </EventsDataTable>
        </Card.Content>
    </Card.Root>
</div>

<Sheet.Root open={!!$eventsResponse.data} onOpenChange={() => selectedStackId.set(null)}>
    <Sheet.Content class="w-full overflow-y-auto sm:max-w-full md:w-5/6">
        <Sheet.Header>
            <Sheet.Title>Event Details</Sheet.Title>
        </Sheet.Header>
        <EventsDrawer id={$selectedStackId || ''}></EventsDrawer>
    </Sheet.Content>
</Sheet.Root>
