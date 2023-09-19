<script lang="ts">
	import EventsTable from '$comp/events/EventsTable.svelte';
	import EventsTailLogTable from '$comp/events/EventsTailLogTable.svelte';
	import Summary from '$comp/events/summary/Summary.svelte';
	import TableColumnPicker from '$comp/table/TableColumnPicker.svelte';
	import type { SummaryModel, SummaryTemplateKeys } from '$lib/models/api';
	import { flexRender, type Table } from '@tanstack/svelte-table';
	import type { Readable } from 'svelte/store';
	import { drawerComponent, showDrawer } from "$lib/stores/drawer";
	import { persisted } from "svelte-local-storage-store";

	let liveMode = persisted<boolean>('live', true);
	let currentTable: Readable<Table<SummaryModel<SummaryTemplateKeys>>>;

	function onRowClick({ detail }: CustomEvent<SummaryModel<SummaryTemplateKeys>>) {
		showDrawer.set(true);
		drawerComponent.set(flexRender(Summary, { summary: detail }));
	}

	function onTableChanged({
		detail
	}: CustomEvent<Readable<Table<SummaryModel<SummaryTemplateKeys>>>>) {
		currentTable = detail;
	}
</script>

<svelte:head>
	<title>Exceptionless</title>
</svelte:head>

<div class="justify-center w-full">
	<div class="stats shadow mx-2">
		<div class="stat bg-gray-200">
			<div class="stat-figure text-gray-500">
				<svg
					xmlns="http://www.w3.org/2000/svg"
					fill="none"
					viewBox="0 0 24 24"
					class="inline-block w-8 h-8 stroke-current"
					><path
						stroke-linecap="round"
						stroke-linejoin="round"
						stroke-width="2"
						d="M4.318 6.318a4.5 4.5 0 000 6.364L12 20.364l7.682-7.682a4.5 4.5 0 00-6.364-6.364L12 7.636l-1.318-1.318a4.5 4.5 0 00-6.364 0z"
					></path></svg
				>
			</div>
			<div class="stat-title">Events</div>
			<div class="stat-value">31K</div>
			<div class="stat-desc">Jan 1st - Feb 1st</div>
		</div>
	</div>

	<div class="stats shadow mx-2">
		<div class="stat bg-gray-200">
			<div class="stat-figure text-gray-500">
				<svg
					xmlns="http://www.w3.org/2000/svg"
					fill="none"
					viewBox="0 0 24 24"
					class="inline-block w-8 h-8 stroke-current"
					><path
						stroke-linecap="round"
						stroke-linejoin="round"
						stroke-width="2"
						d="M4.318 6.318a4.5 4.5 0 000 6.364L12 20.364l7.682-7.682a4.5 4.5 0 00-6.364-6.364L12 7.636l-1.318-1.318a4.5 4.5 0 00-6.364 0z"
					></path></svg
				>
			</div>
			<div class="stat-title">Stacks</div>
			<div class="stat-value">4,200</div>
			<div class="stat-desc">↗︎ 400 (22%)</div>
		</div>
	</div>

	<div class="stats shadow mx-2">
		<div class="stat bg-gray-200">
			<div class="stat-figure text-gray-500">
				<svg
					xmlns="http://www.w3.org/2000/svg"
					fill="none"
					viewBox="0 0 24 24"
					class="inline-block w-8 h-8 stroke-current"
					><path
						stroke-linecap="round"
						stroke-linejoin="round"
						stroke-width="2"
						d="M4.318 6.318a4.5 4.5 0 000 6.364L12 20.364l7.682-7.682a4.5 4.5 0 00-6.364-6.364L12 7.636l-1.318-1.318a4.5 4.5 0 00-6.364 0z"
					></path></svg
				>
			</div>
			<div class="stat-title">New Stacks</div>
			<div class="stat-value">1,200</div>
			<div class="stat-desc">↘︎ 90 (14%)</div>
		</div>
	</div>

	<div class="stats shadow mx-2">
		<div class="stat bg-gray-200">
			<div class="stat-figure text-gray-500">
				<svg
					xmlns="http://www.w3.org/2000/svg"
					fill="none"
					viewBox="0 0 24 24"
					class="inline-block w-8 h-8 stroke-current"
					><path
						stroke-linecap="round"
						stroke-linejoin="round"
						stroke-width="2"
						d="M4.318 6.318a4.5 4.5 0 000 6.364L12 20.364l7.682-7.682a4.5 4.5 0 00-6.364-6.364L12 7.636l-1.318-1.318a4.5 4.5 0 00-6.364 0z"
					></path></svg
				>
			</div>
			<div class="stat-title">Events Per Hour</div>
			<div class="stat-value">1,200</div>
			<div class="stat-desc">↘︎ 90 (14%)</div>
		</div>
	</div>
</div>

<div class="flex justify-between mt-5">
	<h1 class="text-xl">Events</h1>
	<div class="flex">
		<label class="cursor-pointer label">
			<span class="label-text mr-2">Live</span>
			<input type="checkbox" class="toggle toggle-primary" bind:checked={$liveMode} />
		</label>
		{#if currentTable}
			<div class="ml-1 mt-2">
				<TableColumnPicker table={currentTable}></TableColumnPicker>
			</div>
		{/if}
	</div>
</div>

{#if $liveMode}
	<EventsTailLogTable on:rowclick={onRowClick} on:table={onTableChanged}></EventsTailLogTable>
{:else}
	<EventsTable on:rowclick={onRowClick} on:table={onTableChanged}></EventsTable>
{/if}
