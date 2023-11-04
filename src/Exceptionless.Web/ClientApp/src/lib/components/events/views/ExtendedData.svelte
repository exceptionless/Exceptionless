<script lang="ts">
	import type { PersistentEvent, ViewProject } from '$lib/models/api';
	import ExtendedDataItem from '../ExtendedDataItem.svelte';
	import { getExtendedDataItems } from '$lib/helpers/persistent-event';

	export let event: PersistentEvent;
	export let project: ViewProject | undefined;

	const items = getExtendedDataItems(event, project);
</script>

{#each items as { title, promoted, data }}
	{#if promoted === false}
		<ExtendedDataItem {title} {data}></ExtendedDataItem>
	{/if}
{/each}
