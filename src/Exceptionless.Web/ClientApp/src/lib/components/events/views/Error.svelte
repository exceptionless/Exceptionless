<script lang="ts">
	import * as Table from '$comp/ui/table';
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

<Table.Root>
	<Table.Body>
		<Table.Row>
			<Table.Head class="whitespace-nowrap">Error Type</Table.Head>
			<Table.Cell
				><ClickableStringFilter term="error.type" value={errorType}
					>{errorType}</ClickableStringFilter
				></Table.Cell
			>
		</Table.Row>
		{#if message}
			<Table.Row>
				<Table.Head class="whitespace-nowrap">Message</Table.Head>
				<Table.Cell
					><ClickableStringFilter term="error.message" value={message}
						>{message}</ClickableStringFilter
					></Table.Cell
				>
			</Table.Row>
		{/if}
		{#if code}
			<Table.Row>
				<Table.Head class="whitespace-nowrap">Code</Table.Head>
				<Table.Cell
					><ClickableVersionFilter term="error.code" value={code}
						>{code}</ClickableVersionFilter
					></Table.Cell
				>
			</Table.Row>
		{/if}
		{#if submissionMethod}
			<Table.Row>
				<Table.Head class="whitespace-nowrap">Submission Method</Table.Head>
				<Table.Cell>{submissionMethod}</Table.Cell>
			</Table.Row>
		{/if}</Table.Body
	>
</Table.Root>

<div class="flex justify-between mt-4 mb-2">
	<h4 class="text-lg">Stack Trace</h4>
	<div class="flex justify-end">
		<CopyToClipboardButton title="Copy Stack Trace to Clipboard" value={stackTrace}
		></CopyToClipboardButton>
	</div>
</div>
<div class="overflow-auto p-2 mt-2 text-xs">
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
	<Table.Root>
		<Table.Header>
			<Table.Row>
				<Table.Head>Name</Table.Head>
				<Table.Head>Version</Table.Head>
			</Table.Row>
		</Table.Header>
		<Table.Body>
			{#each modules as module}
				<Table.Row>
					<Table.Cell class="whitespace-nowrap">{module.name}</Table.Cell>
					<Table.Cell>{module.version}</Table.Cell>
				</Table.Row>
			{/each}</Table.Body
		>
	</Table.Root>
{/if}
