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
	import { Button } from '$comp/ui/button';
	import * as Tabs from '$comp/ui/tabs';

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

	<Tabs.Root value={activeTab} class="mt-4 mb-4">
		<Tabs.List class="mb-4">
			{#each $tabs as tab}
				<Tabs.Trigger value={tab}>{tab}</Tabs.Trigger>
			{/each}
		</Tabs.List>
		<Tabs.Content value="Overview">
			<Overview event={$eventResponse.data}></Overview>
		</Tabs.Content>
		<Tabs.Content value="Exception">
			<Error event={$eventResponse.data}></Error>
		</Tabs.Content>
		<Tabs.Content value="Environment">
			<Environment event={$eventResponse.data}></Environment>
		</Tabs.Content>
		<Tabs.Content value="Request">
			<Request event={$eventResponse.data}></Request>
		</Tabs.Content>
		<Tabs.Content value="Trace Log">
			<TraceLog logs={$eventResponse.data.data?.['@trace']}></TraceLog>
		</Tabs.Content>
		<Tabs.Content value="Extended Data">
			<ExtendedData
				event={$eventResponse.data}
				project={$projectResponse.data}
				on:promoted={onPromoted}
			></ExtendedData>
		</Tabs.Content>
		<Tabs.Content value={activeTab}>
			<PromotedExtendedData
				title={activeTab || ''}
				event={$eventResponse.data}
				on:demoted={onDemoted}
			></PromotedExtendedData>
		</Tabs.Content>
	</Tabs.Root>

	<Button class="flex justify-center" href="/event/{id}">View Event</Button>
{:else}
	<ErrorMessage message={$eventResponse.error?.errors.general}></ErrorMessage>
{/if}
