<script lang="ts">
	import * as Sheet from '$lib/components/ui/sheet';
	import EventsTable from '$comp/events/table/EventsTable.svelte';
	import EventsTailLogTable from '$comp/events/table/EventsTailLogTable.svelte';
	import TableColumnPicker from '$comp/table/TableColumnPicker.svelte';
	import type { SummaryModel, SummaryTemplateKeys } from '$lib/models/api';
	import { persisted } from 'svelte-local-storage-store';
	import CustomEventMessage from '$comp/messaging/CustomEventMessage.svelte';
	import {
		type IFilter,
		updateFilters,
		parseFilter,
		FilterSerializer,
		toFilter
	} from '$comp/filters/filters';
	import { derived } from 'svelte/store';

	import SearchInput from '$comp/SearchInput.svelte';
	import DateRangeDropdown from '$comp/DateRangeDropdown.svelte';
	import { showDrawer } from '$lib/stores/drawer';
	import EventsDrawer from '$comp/events/EventsDrawer.svelte';
	import Switch from '$comp/primitives/Switch.svelte';

	let liveMode = persisted<boolean>('live', true);
	let hideDrawer = true;

	function onRowClick({ detail }: CustomEvent<SummaryModel<SummaryTemplateKeys>>) {
		hideDrawer = false;
	}

	let time = persisted<string>('time', '');

	const filters = persisted<IFilter[]>('filters', [], { serializer: new FilterSerializer() });
	let filter = derived(filters, ($filters) => toFilter($filters));
	function onFilterChanged({ detail }: CustomEvent<IFilter>): void {
		filters.set(updateFilters($filters, detail));
	}

	let parseFiltersDebounceTimer: ReturnType<typeof setTimeout>;
	function onFilterInputChanged(event: Event) {
		clearTimeout(parseFiltersDebounceTimer);
		parseFiltersDebounceTimer = setTimeout(() => {
			const { value } = event.target as HTMLInputElement;
			filters.set(parseFilter($filters, value));
		}, 500);
	}
</script>

<svelte:head>
	<title>Exceptionless</title>
</svelte:head>

<CustomEventMessage type="filter" on:message={onFilterChanged}></CustomEventMessage>

<!--<Card padding="sm" class="bg-white rounded-lg dark:bg-gray-800">-->
<div class="flex justify-between items-center mb-4">
	<div>
		<h3 class="mb-2 text-xl font-bold text-gray-900 dark:text-white">Events</h3>
	</div>
</div>

{#if $liveMode}
	<EventsTailLogTable on:rowclick={onRowClick} {filter}>
		<div slot="header" let:table>
			<div class="flex justify-between items-center pb-4">
				<div class="w-2/4">
					<SearchInput value={$filter} onChanged={onFilterInputChanged} />
				</div>
				<div class="flex items-center space-x-2">
					<Switch id="live-mode" bind:checked={$liveMode}>Live</Switch>
					<TableColumnPicker {table}></TableColumnPicker>
				</div>
			</div>
		</div>
	</EventsTailLogTable>
{:else}
	<EventsTable on:rowclick={onRowClick} {filter} {time}>
		<div slot="header" let:table>
			<div class="flex justify-between items-center pb-4">
				<div class="w-2/4">
					<SearchInput value={$filter} onChanged={onFilterInputChanged} />
				</div>
				<DateRangeDropdown bind:value={$time}></DateRangeDropdown>
				<div class="flex items-center space-x-2">
					<Switch id="live-mode" bind:checked={$liveMode}>Live</Switch>
					<TableColumnPicker {table}></TableColumnPicker>
				</div>
			</div>
		</div>
	</EventsTable>
{/if}
<!--</Card>-->

<Sheet.Root bind:open={$showDrawer}>
	<Sheet.Trigger>Open</Sheet.Trigger>
	<Sheet.Content>
		<Sheet.Header>
			<Sheet.Title>Event Details</Sheet.Title>
			<Sheet.Description>
				<EventsDrawer id={''}></EventsDrawer>
			</Sheet.Description>
		</Sheet.Header>
	</Sheet.Content>
</Sheet.Root>
