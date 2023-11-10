<script lang="ts">
	import type { PersistentEvent } from '$lib/models/api';
	import {
		getErrorData,
		getErrorType,
		getMessage,
		getStackTrace
	} from '$lib/helpers/persistent-event';
	import SimpleStackTrace from '../SimpleStackTrace.svelte';
	import StackTrace from '../StackTrace.svelte';
	import ClickableStringFilter from '$comp/filters/ClickableStringFilter.svelte';
	import ClickableVersionFilter from '$comp/filters/ClickableVersionFilter.svelte';
	import CopyToClipboardButton from '$comp/CopyToClipboardButton.svelte';
	import ExtendedDataItem from '../ExtendedDataItem.svelte';

	export let event: PersistentEvent;

	const errorData = getErrorData(event);
	const errorType = getErrorType(event);
	const stackTrace = getStackTrace(event);

	const code = event.data?.['@error']?.code;
	const message = getMessage(event);
	const modules = event.data?.['@error']?.modules || [];
	const submissionMethod = event.data?.['@submission_method'];
</script>

<table class="table table-zebra table-xs border border-base-300">
	<tbody>
		<tr>
			<th class="border border-base-300 whitespace-nowrap">Error Type</th>
			<td class="border border-base-300"
				><ClickableStringFilter term="error.type" value={errorType}
					>{errorType}</ClickableStringFilter
				></td
			>
		</tr>
		{#if message}
			<tr>
				<th class="border border-base-300 whitespace-nowrap">Message</th>
				<td class="border border-base-300"
					><ClickableStringFilter term="error.message" value={message}
						>{message}</ClickableStringFilter
					></td
				>
			</tr>
		{/if}
		{#if code}
			<tr>
				<th class="border border-base-300 whitespace-nowrap">Code</th>
				<td class="border border-base-300"
					><ClickableVersionFilter term="error.code" value={code}
						>{code}</ClickableVersionFilter
					></td
				>
			</tr>
		{/if}
		{#if submissionMethod}
			<tr>
				<th class="border border-base-300 whitespace-nowrap">Submission Method</th>
				<td class="border border-base-300">{submissionMethod}</td>
			</tr>
		{/if}</tbody
	>
</table>

<div class="flex justify-between mt-4 mb-2">
	<h4 class="text-lg">Stack Trace</h4>
	<div class="flex justify-end">
		<CopyToClipboardButton title="Copy Stack Trace to Clipboard" value={stackTrace}
		></CopyToClipboardButton>
	</div>
</div>
<div class="overflow-auto p-2 mt-2 border border-base-300 text-xs">
	{#if event.data?.['@error']}
		<StackTrace error={event.data['@error']} />
	{:else}
		<SimpleStackTrace error={event.data?.['@simple_error']} />
	{/if}
</div>

{#each errorData as ed}
	<div class="mt-2">
		<ExtendedDataItem canPromote={false} title={ed.title} data={ed.data}></ExtendedDataItem>
	</div>
{/each}

{#if modules.length}
	<div class="flex justify-between items-center mt-4 mb-2">
		<h4 class="text-lg">Modules</h4>
	</div>
	<table class="table table-zebra table-xs border border-base-300">
		<thead>
			<tr>
				<th class="border border-base-300">Name</th>
				<th class="border border-base-300">Version</th>
			</tr>
		</thead>
		<tbody>
			{#each modules as module}
				<tr>
					<td class="border border-base-300 whitespace-nowrap">{module.name}</td>
					<td class="border border-base-300">{module.version}</td>
				</tr>
			{/each}</tbody
		>
	</table>
{/if}
