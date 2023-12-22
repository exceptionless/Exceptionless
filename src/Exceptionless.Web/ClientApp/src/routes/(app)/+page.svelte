<script lang="ts">
	import EventsDataTable from '$comp/events/table/EventsDataTable.svelte';
	import * as Card from '$comp/ui/card';
	import { writable } from 'svelte/store';

	let filter = writable('');
	let time = writable('');
</script>

<svelte:head>
	<title>Exceptionless</title>
</svelte:head>

<Card.Root>
	<Card.Title class="p-6 pb-4 text-xl font-bold">Tasks</Card.Title>
	<Card.Content><EventsDataTable {filter} {time}></EventsDataTable></Card.Content></Card.Root
>
