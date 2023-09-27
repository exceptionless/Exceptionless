<script lang="ts">
	import {
		type FetchClientResponse,
		globalFetchClient as api,
		globalLoading as loading
	} from '$api/FetchClient';
	import Accordion from '$comp/accordion/Accordion.svelte';
	import AccordionItem from '$comp/accordion/AccordionItem.svelte';
	import type { PersistentEvent } from '$lib/models/api';
	import Overview from './views/Overview.svelte';

	export let id: string;
	let response: FetchClientResponse<PersistentEvent>;

	async function loadData() {
		response = await api.getJSON<PersistentEvent>(`events/${id}`);
	}

	loadData();
</script>

{#if $loading}
	<p>Loading...</p>
{:else if response?.data}
	<Accordion>
		<AccordionItem title="Overview" checked={true}>
			<Overview event={response.data}></Overview>
		</AccordionItem>
		<AccordionItem title="Exception">
			<p>hello</p>
		</AccordionItem>
		<AccordionItem title="Environment">
			<p>hello</p>
		</AccordionItem>
		<AccordionItem title="Extended Data">
			<p>hello</p>
		</AccordionItem>
	</Accordion>
	<pre>{JSON.stringify(response.data, null, 2)}</pre>
{:else}
	<p>Error: {response}</p>
{/if}
