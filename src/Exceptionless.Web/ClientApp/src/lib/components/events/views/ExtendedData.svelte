<script lang="ts">
	import type { PersistentEvent } from '$lib/models/api';
	import DateTime from '$comp/formatters/DateTime.svelte';
	import TimeAgo from '$comp/formatters/TimeAgo.svelte';
	import ClickableDateFilter from '$comp/filters/ClickableDateFilter.svelte';
	import ExtendedDataItem from '../ExtendedDataItem.svelte';
	import { getExtendedDataItems } from '$lib/helpers/persistent-event';

	export let event: PersistentEvent;

	const items = getExtendedDataItems(event);
</script>

<table class="table table-zebra table-xs border border-base-300">
	<tbody>
		<tr>
			<th class="border border-base-300 whitespace-nowrap">Occurred On</th>
			<td class="border border-base-300"
				><ClickableDateFilter term="date" value={event.date}
					><DateTime value={event.date}></DateTime> (<TimeAgo value={event.date}
					></TimeAgo>)</ClickableDateFilter
				></td
			>
		</tr>
	</tbody>
</table>

{#each [...items] as [title, data]}
	<ExtendedDataItem canPromote={false} {title} {data}></ExtendedDataItem>
{/each}
