<script lang="ts">
	import EventsTable from '$comp/events/EventsTable.svelte';
	import EventsTailLogTable from '$comp/events/EventsTailLogTable.svelte';
	import type { SummaryModel, SummaryTemplateKeys } from '$lib/models/api';

	let liveMode = true;
	let showDrawer = false;
	let currentSummary: SummaryModel<SummaryTemplateKeys>;
	async function onRowClick({ detail }: CustomEvent<SummaryModel<SummaryTemplateKeys>>) {
		currentSummary = detail;
		showDrawer = true;
	}
</script>

<svelte:head>
	<title>Exceptionless</title>
</svelte:head>

<div class="drawer drawer-end">
	<input
		id="event-detail-drawer"
		type="checkbox"
		class="drawer-toggle"
		bind:checked={showDrawer}
	/>
	<div class="drawer-content">
		<div class="stats shadow">
			<div class="stat place-items-center">
				<div class="stat-title">Events</div>
				<div class="stat-value">31K</div>
				<div class="stat-desc">Jan 1st - Feb 1st</div>
			</div>

			<div class="stat place-items-center">
				<div class="stat-title">Stacks</div>
				<div class="stat-value">4,200</div>
				<div class="stat-desc">↗︎ 400 (22%)</div>
			</div>

			<div class="stat place-items-center">
				<div class="stat-title">New Stacks</div>
				<div class="stat-value">1,200</div>
				<div class="stat-desc">↘︎ 90 (14%)</div>
			</div>

			<div class="stat place-items-center">
				<div class="stat-title">Events Per Hour</div>
				<div class="stat-value">1,200</div>
				<div class="stat-desc">↘︎ 90 (14%)</div>
			</div>
		</div>
		<div class="flex justify-between mt-5">
			<h1 class="text-xl">Events</h1>
			<label class="cursor-pointer label">
				<span class="label-text mr-2">Live</span>
				<input type="checkbox" class="toggle toggle-primary" bind:checked={liveMode} />
			</label>
		</div>

		{#if liveMode}
			<EventsTailLogTable on:rowclick={onRowClick}></EventsTailLogTable>
		{:else}
			<EventsTable on:rowclick={onRowClick}></EventsTable>
		{/if}
	</div>
	<div class="drawer-side">
		<label for="event-detail-drawer" class="drawer-overlay"></label>
		<ul class="menu p-4 w-80 min-h-full bg-base-200 text-base-content">
			currentSummary: {JSON.stringify(currentSummary)}
		</ul>
	</div>
</div>
