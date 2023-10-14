<script lang="ts">
	import EventsTable from '$comp/events/table/EventsTable.svelte';
	import EventsTailLogTable from '$comp/events/table/EventsTailLogTable.svelte';
	import TableColumnPicker from '$comp/table/TableColumnPicker.svelte';
	import type { SummaryModel, SummaryTemplateKeys } from '$lib/models/api';
	import { drawerComponent, drawerComponentProps, showDrawer } from '$lib/stores/drawer';
	import { persisted } from 'svelte-local-storage-store';
	import EventsDrawer from '$comp/events/EventsDrawer.svelte';
	import CustomEventMessage from '$comp/messaging/CustomEventMessage.svelte';
	import {
		toFilter,
		type IFilter,
		updateFilters,
		parseFilter
	} from '$comp/filters/filters';
	import { derived } from 'svelte/store';

	let liveMode = persisted<boolean>('live', true);

	function onRowClick({ detail }: CustomEvent<SummaryModel<SummaryTemplateKeys>>) {
		showDrawer.set(true);
		drawerComponent.set(EventsDrawer);
		drawerComponentProps.set({ id: detail.id });
	}

	let time = persisted<string>('time', '');

	const filters = persisted<IFilter[]>('filters', []);
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

<h1 class="text-xl">Events</h1>

{#if $liveMode}
	<EventsTailLogTable on:rowclick={onRowClick} {filter}>
		<div slot="header" let:table>
			<div class="flex justify-between items-center">
				<input
					type="search"
					placeholder="Search..."
					class="input input-sm w-full max-w-xs"
					value={$filter}
					on:input={onFilterInputChanged}
				/>
				<div class="flex items-center space-x-2">
					<div class="flex items-center">
						<label class="cursor-pointer label">
							<span class="label-text mr-2">Live</span>
							<input
								type="checkbox"
								class="toggle toggle-primary"
								bind:checked={$liveMode}
							/>
						</label>
					</div>
					<TableColumnPicker {table}></TableColumnPicker>
				</div>
			</div>
		</div>
	</EventsTailLogTable>
{:else}
	<EventsTable on:rowclick={onRowClick} {filter} {time}>
		<div slot="header" let:table>
			<div class="flex justify-between items-center">
				<input
					type="search"
					placeholder="Search..."
					class="input input-sm w-full max-w-xs"
					value={$filter}
					on:input={onFilterInputChanged}
				/>
				<select class="select select-sm" bind:value={$time}>
					<option value="last hour">Last Hour</option>
					<option value="last 24 hours">Last 24 Hours</option>
					<option value="last week">Last Week</option>
					<option value="last 30 days">Last 30 Days</option>
					<option value="">All Time</option>
				</select>
				<div class="flex items-center space-x-2">
					<div class="flex items-center">
						<label class="cursor-pointer label">
							<span class="label-text mr-2">Live</span>
							<input
								type="checkbox"
								class="toggle toggle-primary"
								bind:checked={$liveMode}
							/>
						</label>
					</div>
					<TableColumnPicker {table}></TableColumnPicker>
				</div>
			</div>
		</div>
	</EventsTable>
{/if}
