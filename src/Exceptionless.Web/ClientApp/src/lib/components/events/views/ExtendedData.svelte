<script lang="ts">
	import type { PersistentEvent } from '$lib/models/api';
	import ExtendedDataItem from '../ExtendedDataItem.svelte';
	import { getExtendedDataItems } from '$lib/helpers/persistent-event';

	export let event: PersistentEvent;

	const items = getExtendedDataItems(event);
</script>

{#each [...items] as [title, data]}
	<ExtendedDataItem canPromote={false} {title} {data}></ExtendedDataItem>
{/each}
