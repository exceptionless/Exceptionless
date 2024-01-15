<script lang="ts">
	import * as Card from '$comp/ui/card';
	import * as Sheet from '$comp/ui/sheet';
	import EventsTailLogTable from '$comp/events/table/EventsTailLogTable.svelte';
	import TableColumnPicker from '$comp/table/TableColumnPicker.svelte';
	import type { SummaryModel, SummaryTemplateKeys } from '$lib/models/api';
	import CustomEventMessage from '$comp/messaging/CustomEventMessage.svelte';
	import {
		filter,
		filterWithFaceted,
		onFilterChanged,
		onFilterInputChanged
	} from '$lib/stores/events';

	import SearchInput from '$comp/SearchInput.svelte';
	import EventsDrawer from '$comp/events/EventsDrawer.svelte';

	let selectedEventId: string | null = null;
	function onRowClick({ detail }: CustomEvent<SummaryModel<SummaryTemplateKeys>>) {
		selectedEventId = detail.id;
	}
</script>

<svelte:head>
	<title>Exceptionless</title>
</svelte:head>

<CustomEventMessage type="filter" on:message={onFilterChanged}></CustomEventMessage>

<Card.Root>
	<Card.Title class="p-6 pb-4 text-xl font-bold">Event Stream</Card.Title>
	<Card.Content>
		<EventsTailLogTable on:rowclick={onRowClick} filter={filterWithFaceted}>
			<div slot="header" let:table>
				<div class="flex items-center justify-between p-2 pb-4">
					<div class="w-2/4">
						<SearchInput value={$filter} on:input={onFilterInputChanged} />
					</div>
					<div class="flex items-center space-x-2">
						<TableColumnPicker {table}></TableColumnPicker>
					</div>
				</div>
			</div>
		</EventsTailLogTable>
	</Card.Content></Card.Root
>

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
