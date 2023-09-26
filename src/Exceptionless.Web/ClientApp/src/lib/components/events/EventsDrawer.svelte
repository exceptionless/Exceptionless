<script lang="ts">
	import {
		type FetchClientResponse,
		globalFetchClient as api,
		globalLoading as loading
	} from '$api/FetchClient';
	import type { PersistentEvent } from '$lib/models/api.generated';

	export let id: string;
	let response: FetchClientResponse<PersistentEvent>;

	async function loadData() {
		response = await api.getJSON<PersistentEvent>(`events/${id}`);
	}

	loadData();
</script>

{#if $loading}
    <p>Loading...</p>
{:else if response?.ok}
    <pre>{JSON.stringify(response.data, null, 2)}</pre>
{:else}
    <p>Error: {response}</p>
{/if}
