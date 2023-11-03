<script lang="ts">
	import type { PersistentEvent } from '$lib/models/api';
	import DateTime from '$comp/formatters/DateTime.svelte';
	import TimeAgo from '$comp/formatters/TimeAgo.svelte';
	import ClickableStringFilter from '$comp/filters/ClickableStringFilter.svelte';
	import ClickableDateFilter from '$comp/filters/ClickableDateFilter.svelte';
	import ExtendedDataItem from '../ExtendedDataItem.svelte';
	import Bytes from '$comp/formatters/Bytes.svelte';
	import Number from '$comp/formatters/Number.svelte';

	export let event: PersistentEvent;

	const environment = event.data?.['@environment'] ?? {};
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
		{#if environment.machine_name}
			<tr>
				<th class="border border-base-300 whitespace-nowrap">Machine Name</th>
				<td class="border border-base-300"
					><ClickableStringFilter term="machine" value={environment.machine_name}
						>{environment.machine_name}</ClickableStringFilter
					></td
				>
			</tr>
		{/if}
		{#if environment.ip_address}
			<tr>
				<th class="border border-base-300 whitespace-nowrap">IP Address</th>
				<td class="border border-base-300"
					><ClickableStringFilter term="ip" value={environment.ip_address}
						>{environment.ip_address}</ClickableStringFilter
					></td
				>
			</tr>
		{/if}
		{#if environment.processor_count}
			<tr>
				<th class="border border-base-300 whitespace-nowrap">Processor Count</th>
				<td class="border border-base-300"
					><Number value={environment.processor_count}></Number></td
				>
			</tr>
		{/if}
		{#if environment.total_physical_memory}
			<tr>
				<th class="border border-base-300 whitespace-nowrap">Total Memory</th>
				<td class="border border-base-300"
					><Bytes value={environment.total_physical_memory}></Bytes></td
				>
			</tr>
		{/if}
		{#if environment.available_physical_memory}
			<tr>
				<th class="border border-base-300 whitespace-nowrap">Available Memory</th>
				<td class="border border-base-300"
					><Bytes value={environment.available_physical_memory}></Bytes></td
				>
			</tr>
		{/if}
		{#if environment.process_memory_size}
			<tr>
				<th class="border border-base-300 whitespace-nowrap">Process Memory</th>
				<td class="border border-base-300"
					><Bytes value={environment.process_memory_size}></Bytes></td
				>
			</tr>
		{/if}
		{#if environment.o_s_name}
			<tr>
				<th class="border border-base-300 whitespace-nowrap">OS Name</th>
				<td class="border border-base-300"
					><ClickableStringFilter term="os" value={environment.o_s_name}
						>{environment.o_s_name}</ClickableStringFilter
					></td
				>
			</tr>
		{/if}
		{#if environment.o_s_version}
			<tr>
				<th class="border border-base-300 whitespace-nowrap">OS Version</th>
				<td class="border border-base-300"
					><ClickableStringFilter term="os.version" value={environment.o_s_version}
						>{environment.o_s_version}</ClickableStringFilter
					></td
				>
			</tr>
		{/if}
		{#if environment.architecture}
			<tr>
				<th class="border border-base-300 whitespace-nowrap">Architecture</th>
				<td class="border border-base-300"
					><ClickableStringFilter term="architecture" value={environment.architecture}
						>{environment.architecture}</ClickableStringFilter
					></td
				>
			</tr>
		{/if}
		{#if environment.runtime_version}
			<tr>
				<th class="border border-base-300 whitespace-nowrap">Runtime Version</th>
				<td class="border border-base-300">{environment.runtime_version}</td>
			</tr>
		{/if}
		{#if environment.process_id}
			<tr>
				<th class="border border-base-300 whitespace-nowrap">Process ID</th>
				<td class="border border-base-300">{environment.process_id}</td>
			</tr>
		{/if}
		{#if environment.process_name}
			<tr>
				<th class="border border-base-300 whitespace-nowrap">Process Name</th>
				<td class="border border-base-300">{environment.process_name}</td>
			</tr>
		{/if}
		{#if environment.command_line}
			<tr>
				<th class="border border-base-300 whitespace-nowrap">Command Line</th>
				<td class="border border-base-300"
					><span class="inline line-clamp-2">{environment.command_line}</span></td
				>
			</tr>
		{/if}
	</tbody>
</table>

{#if environment.data}
	<div class="mt-2">
		<ExtendedDataItem canPromote={false} title="Additional Data" data={environment.data}
		></ExtendedDataItem>
	</div>
{/if}
