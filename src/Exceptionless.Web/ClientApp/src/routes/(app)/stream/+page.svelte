<script lang="ts">
    import * as Card from '$comp/ui/card';
    import * as Sheet from '$comp/ui/sheet';
    import EventsTailLogDataTable from '$comp/events/table/EventsTailLogDataTable.svelte';
    import type { SummaryModel, SummaryTemplateKeys } from '$lib/models/api';
    import CustomEventMessage from '$comp/messaging/CustomEventMessage.svelte';
    import { filter, onFilterChanged } from '$lib/stores/events';

    import EventsDrawer from '$comp/events/EventsDrawer.svelte';

    let selectedEventId: string | null = null;
    function onRowClick({ detail }: CustomEvent<SummaryModel<SummaryTemplateKeys>>) {
        selectedEventId = detail.id;
    }
</script>

<CustomEventMessage type="filter" on:message={onFilterChanged}></CustomEventMessage>

<Card.Root>
    <Card.Title tag="h2" class="p-6 pb-4 text-2xl">Event Stream</Card.Title>
    <Card.Content>
        <EventsTailLogDataTable on:rowclick={onRowClick} {filter}></EventsTailLogDataTable>
    </Card.Content></Card.Root
>

<Sheet.Root open={!!selectedEventId} onOpenChange={() => (selectedEventId = null)}>
    <Sheet.Content class="w-full overflow-y-auto sm:max-w-full md:w-5/6">
        <Sheet.Header>
            <Sheet.Title>Event Details</Sheet.Title>
        </Sheet.Header>
        <EventsDrawer id={selectedEventId || ''}></EventsDrawer>
    </Sheet.Content>
</Sheet.Root>
