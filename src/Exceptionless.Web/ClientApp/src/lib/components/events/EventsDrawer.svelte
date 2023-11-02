<script lang="ts">
	import {
		type FetchClientResponse,
		globalFetchClient as api,
		globalLoading as loading
	} from '$api/FetchClient';
	import ErrorMessage from '$comp/ErrorMessage.svelte';
	import { hasErrorOrSimpleError } from '$lib/helpers/persistent-event';
	import type { PersistentEvent } from '$lib/models/api';
	import { writable, type Writable } from 'svelte/store';
	import Error from './views/Error.svelte';
	import Overview from './views/Overview.svelte';

	export let id: string;
	let response: FetchClientResponse<PersistentEvent>;

	type TabType = 'Overview' | 'Exception' | string | null;
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
