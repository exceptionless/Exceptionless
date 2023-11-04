<script lang="ts">
	import type { PersistentEvent } from '$lib/models/api';
	import ExtendedDataItem from '../ExtendedDataItem.svelte';

	export let event: PersistentEvent;
	export let title: string;
</script>

<ExtendedDataItem {title} isPromoted={true} data={event.data?.[title]}></ExtendedDataItem>
