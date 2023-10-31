<script lang="ts">
	import {
		type FetchClientResponse,
		globalFetchClient as api,
		globalLoading as loading
	} from '$api/FetchClient';
	import ErrorMessage from '$comp/ErrorMessage.svelte';
	import type { PersistentEvent } from '$lib/models/api';
	import Overview from './views/Overview.svelte';

	export let id: string;
	let response: FetchClientResponse<PersistentEvent>;

	async function loadData() {
		response = await api.getJSON<PersistentEvent>(`events/${id}`);
	}

	loadData();
</script>

{#if response?.data}
	<h1 class="text-xl">Event Details</h1>
	<Overview event={response.data}></Overview>
	<div class="flex justify-center mt-2">
		<a href="/event/{id}" class="btn btn-primary btn-sm"> View Event </a>
	</div>
{:else if $loading}
	<p>Loading...</p>
{:else}
	<ErrorMessage message={response?.problem?.errors.general}></ErrorMessage>
{/if}
