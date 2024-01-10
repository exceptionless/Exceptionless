<script lang="ts">
	import { persisted } from 'svelte-local-storage-store';
	import { writable } from 'svelte/store';

	import * as Card from '$comp/ui/card';
	import * as Sheet from '$comp/ui/sheet';
	import EventsDataTable from '$comp/events/table/EventsDataTable.svelte';
	import EventsDrawer from '$comp/events/EventsDrawer.svelte';
	import type { SummaryModel, SummaryTemplateKeys } from '$lib/models/api';

	let filter = writable('');
	let time = persisted<string>('filter.time', '');

	let selectedEventId: string | null = null;
	function onRowClick({ detail }: CustomEvent<SummaryModel<SummaryTemplateKeys>>) {
		selectedEventId = detail.id;
	}
</script>

<svelte:head>
	<title>Exceptionless</title>
</svelte:head>

<Card.Root>
	<Card.Title class="p-6 pb-4 text-xl font-bold">Tasks</Card.Title>
	<Card.Content>
		<EventsDataTable {filter} {time} on:rowclick={onRowClick}></EventsDataTable>
	</Card.Content>
</Card.Root>

<Sheet.Root open={!!selectedEventId} onOpenChange={() => (selectedEventId = null)}>
	<Sheet.Content class="w-full md:w-5/6 sm:max-w-full">
		<Sheet.Header>
			<Sheet.Title>Event Details</Sheet.Title>
			<Sheet.Description>
				<EventsDrawer id={selectedEventId || ''}></EventsDrawer>
			</Sheet.Description>
		</Sheet.Header>
	</Sheet.Content>
</Sheet.Root>
