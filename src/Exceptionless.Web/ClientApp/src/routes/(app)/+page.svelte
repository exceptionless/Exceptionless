<script lang="ts">
	import EventsTable from '$comp/events/table/EventsTable.svelte';
	import EventsTailLogTable from '$comp/events/table/EventsTailLogTable.svelte';
	import TableColumnPicker from '$comp/table/TableColumnPicker.svelte';
	import type { SummaryModel, SummaryTemplateKeys } from '$lib/models/api';
	import { drawerComponent, drawerComponentProps, showDrawer } from '$lib/stores/drawer';
	import { persisted } from 'svelte-local-storage-store';
	import EventsDrawer from '$comp/events/EventsDrawer.svelte';

	let liveMode = persisted<boolean>('live', true);

	function onRowClick({ detail }: CustomEvent<SummaryModel<SummaryTemplateKeys>>) {
		showDrawer.set(true);
		drawerComponent.set(EventsDrawer);
		drawerComponentProps.set({ id: detail.id });
	}

	let filter = persisted<string>('filter', '');
	let time = persisted<string>('time', '');
</script>

<svelte:head>
	<title>Exceptionless</title>
</svelte:head>

<h1 class="text-xl">Events</h1>

{#if $liveMode}
	<EventsTailLogTable on:rowclick={onRowClick} {filter}>
		<div slot="header" let:table>
			<div class="flex justify-between items-center">
				<input
					type="search"
					placeholder="Search..."
					class="input input-sm w-full max-w-xs"
					bind:value={$filter}
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
					bind:value={$filter}
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
