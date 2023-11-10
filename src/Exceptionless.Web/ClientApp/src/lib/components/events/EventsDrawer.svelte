<script lang="ts">
	import ErrorMessage from '$comp/ErrorMessage.svelte';
	import { getExtendedDataItems, hasErrorOrSimpleError } from '$lib/helpers/persistent-event';
	import type { PersistentEvent, ViewProject } from '$lib/models/api';
	import { writable, type Writable } from 'svelte/store';
	import Error from './views/Error.svelte';
	import Overview from './views/Overview.svelte';
	import Environment from './views/Environment.svelte';
	import Request from './views/Request.svelte';
	import TraceLog from './views/TraceLog.svelte';
	import ExtendedData from './views/ExtendedData.svelte';
	import { getEventByIdQuery } from '$api/queries/events';
	import DateTime from '$comp/formatters/DateTime.svelte';
	import ClickableDateFilter from '$comp/filters/ClickableDateFilter.svelte';
	import TimeAgo from '$comp/formatters/TimeAgo.svelte';
	import { getProjectByIdQuery } from '$api/queries/projects';
	import { getStackByIdQuery } from '$api/queries/stacks';
	import ClickableStringFilter from '$comp/filters/ClickableStringFilter.svelte';
	import PromotedExtendedData from './views/PromotedExtendedData.svelte';

	export let id: string;

	type TabType =
		| 'Overview'
		| 'Exception'
		| 'Environment'
		| 'Request'
		| 'Trace Log'
		| 'Extended Data'
		| string
		| null;
	let activeTab: TabType = null;
	const tabs: Writable<TabType[]> = writable([]);
	tabs.subscribe((items) => {
		if (!items) {
			activeTab = null;
		}

		if (!items.includes(activeTab)) {
			activeTab = items[0];
		}
	});

	function getTabs(event?: PersistentEvent | null, project?: ViewProject): TabType[] {
		if (!event) {
			return [];
		}

		const tabs = ['Overview'];
		if (hasErrorOrSimpleError(event)) {
			tabs.push('Exception');
		}

		if (event.data?.['@environment']) {
			tabs.push('Environment');
		}

		if (event.data?.['@request']) {
			tabs.push('Request');
		}

		if (event.data?.['@trace']) {
			tabs.push('Trace Log');
		}

		if (project) {
			const extendedDataItems = getExtendedDataItems(event, project);
			let hasExtendedData = false;

			for (const item of extendedDataItems) {
				if (item.promoted) {
					tabs.push(item.title);
				} else {
					hasExtendedData = true;
				}
			}

			if (hasExtendedData) {
				tabs.push('Extended Data');
			}
		}

		return tabs;
	}

	const projectId = writable<string | null>(null);
	const projectResponse = getProjectByIdQuery(projectId);

	const stackId = writable<string | null>(null);
	const stackResponse = getStackByIdQuery(stackId);

	const eventResponse = getEventByIdQuery(id);
	eventResponse.subscribe((response) => {
		projectId.set(response.data?.project_id ?? null);
		stackId.set(response.data?.stack_id ?? null);
		tabs.set(getTabs(response.data, $projectResponse.data));
	});

	projectResponse.subscribe((response) => {
		tabs.set(getTabs($eventResponse.data, response.data));
	});

	function onPromoted({ detail }: CustomEvent<string>): void {
		tabs.update((items) => {
			items.splice(items.length - 1, 0, detail);
			return items;
		});
		activeTab = detail;
	}

	function onDemoted({ detail }: CustomEvent<string>): void {
		tabs.update((items) => {
			items.splice(items.indexOf(detail), 1);

			if (!items.includes('Extended Data')) {
				items.push('Extended Data');
			}

			return items;
		});
		activeTab = 'Extended Data';
	}
</script>

{#if $eventResponse.isLoading}
	<p>Loading...</p>
{:else if $eventResponse.isSuccess}
	<h1 class="text-xl">Event Details</h1>

	<table class="table table-zebra table-xs border border-base-300 mt-4">
		<tbody>
			<tr>
				<th class="border border-base-300 whitespace-nowrap">Occurred On</th>
				<td class="border border-base-300"
					><ClickableDateFilter term="date" value={$eventResponse.data.date}
						><DateTime value={$eventResponse.data.date}></DateTime> (<TimeAgo
							value={$eventResponse.data.date}
						></TimeAgo>)</ClickableDateFilter
					></td
				>
			</tr>
			{#if $projectResponse.data}
				<tr>
					<th class="border border-base-300 whitespace-nowrap">Project</th>
					<td class="border border-base-300"
						><ClickableStringFilter term="project" value={$projectResponse.data.id}
							>{$projectResponse.data.name}</ClickableStringFilter
						></td
					>
				</tr>
			{/if}
			{#if $stackResponse.data}
				<tr>
					<th class="border border-base-300 whitespace-nowrap">Stack</th>
					<td class="border border-base-300"
						><ClickableStringFilter term="stack" value={$stackResponse.data.id}
							>{$stackResponse.data.title}</ClickableStringFilter
						></td
					>
				</tr>
			{/if}
		</tbody>
	</table>

	<div class="tabs mt-4">
		{#each $tabs as tab}
			<button
				class="tab tab-bordered"
				class:tab-active={activeTab === tab}
				on:click={() => (activeTab = tab)}
				title="Select {tab}">{tab}</button
			>
		{/each}
	</div>

	<div class="mt-4">
		{#if activeTab === 'Overview'}
			<Overview event={$eventResponse.data}></Overview>
		{:else if activeTab === 'Exception'}
			<Error event={$eventResponse.data}></Error>
		{:else if activeTab === 'Environment'}
			<Environment event={$eventResponse.data}></Environment>
		{:else if activeTab === 'Request'}
			<Request event={$eventResponse.data}></Request>
		{:else if activeTab === 'Trace Log'}
			<TraceLog logs={$eventResponse.data.data?.['@trace']}></TraceLog>
		{:else if activeTab === 'Extended Data'}
			<ExtendedData
				event={$eventResponse.data}
				project={$projectResponse.data}
				on:promoted={onPromoted}
			></ExtendedData>
		{:else if !!activeTab}
			<PromotedExtendedData
				title={activeTab}
				event={$eventResponse.data}
				on:demoted={onDemoted}
			></PromotedExtendedData>
		{/if}
	</div>

	<div class="flex justify-center mt-4">
		<a href="/event/{id}" class="btn btn-primary btn-sm">View Event</a>
	</div>
{:else}
	<ErrorMessage message={$eventResponse.error?.errors.general}></ErrorMessage>
{/if}
