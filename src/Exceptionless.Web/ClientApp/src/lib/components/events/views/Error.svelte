<script lang="ts">
    import * as Table from '$comp/ui/table';
    import type { PersistentEvent } from '$lib/models/api';
    import { getErrorData, getErrorType, getMessage, getStackTrace } from '$lib/helpers/persistent-event';
    import SimpleStackTrace from '../SimpleStackTrace.svelte';
    import StackTrace from '../StackTrace.svelte';
    import ClickableStringFilter from '$comp/filters/ClickableStringFilter.svelte';
    import ClickableVersionFilter from '$comp/filters/ClickableVersionFilter.svelte';
    import CopyToClipboardButton from '$comp/CopyToClipboardButton.svelte';
    import ExtendedDataItem from '../ExtendedDataItem.svelte';
    import H4 from '$comp/typography/H4.svelte';

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
            <Table.Cell class="flex items-center">{errorType}<ClickableStringFilter term="error.type" value={errorType} /></Table.Cell>
        </Table.Row>
        {#if message}
            <Table.Row>
                <Table.Head class="whitespace-nowrap">Message</Table.Head>
                <Table.Cell class="flex items-center">{message}<ClickableStringFilter term="error.message" value={message} /></Table.Cell>
            </Table.Row>
        {/if}
        {#if code}
            <Table.Row>
                <Table.Head class="whitespace-nowrap">Code</Table.Head>
                <Table.Cell class="flex items-center">{code}<ClickableVersionFilter term="error.code" value={code} /></Table.Cell>
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

<div class="mb-2 mt-4 flex justify-between">
    <H4>Stack Trace</H4>
    <div class="flex justify-end">
        <CopyToClipboardButton title="Copy Stack Trace to Clipboard" value={stackTrace}></CopyToClipboardButton>
    </div>
</div>
<div class="mt-2 overflow-auto p-2 text-xs">
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
    <div class="mb-2 mt-4 flex items-center justify-between">
        <H4>Modules</H4>
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
