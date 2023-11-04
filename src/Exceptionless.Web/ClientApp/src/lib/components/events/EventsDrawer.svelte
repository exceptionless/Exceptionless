<script lang="ts">
	import {
		type FetchClientResponse,
		globalFetchClient as api,
		globalLoading as loading
	} from '$api/FetchClient';
	import ErrorMessage from '$comp/ErrorMessage.svelte';
	import { getExtendedDataItems, hasErrorOrSimpleError } from '$lib/helpers/persistent-event';
	import type { PersistentEvent } from '$lib/models/api';
	import { writable, type Writable } from 'svelte/store';
	import Error from './views/Error.svelte';
	import Overview from './views/Overview.svelte';
	import Environment from './views/Environment.svelte';
	import Request from './views/Request.svelte';
	import TraceLog from './views/TraceLog.svelte';
	import ExtendedData from './views/ExtendedData.svelte';

	export let id: string;
	let response: FetchClientResponse<PersistentEvent>;

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

	function getTabs(event?: PersistentEvent | null): TabType[] {
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

		const extendedDataItems = getExtendedDataItems(event);
		if (extendedDataItems.size > 0) {
			tabs.push('Extended Data');
		}

		return tabs;
	}

	async function loadData() {
		response = await api.getJSON<PersistentEvent>(`events/${id}`);
		tabs.set(getTabs(response?.data));
	}

	loadData();
</script>

{#if response?.data}
	<h1 class="text-xl">Event Details</h1>

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
			<Overview event={response.data}></Overview>
		{:else if activeTab === 'Exception'}
			<Error event={response.data}></Error>
		{:else if activeTab === 'Environment'}
			<Environment event={response.data}></Environment>
		{:else if activeTab === 'Request'}
			<Request event={response.data}></Request>
		{:else if activeTab === 'Trace Log'}
			<TraceLog event={response.data}></TraceLog>
		{:else if activeTab === 'Extended Data'}
			<ExtendedData event={response.data}></ExtendedData>
		{/if}
	</div>

	<div class="flex justify-center mt-4">
		<a href="/event/{id}" class="btn btn-primary btn-sm">View Event</a>
	</div>
{:else if $loading}
	<p>Loading...</p>
{:else}
	<ErrorMessage message={response?.problem?.errors.general}></ErrorMessage>
{/if}
