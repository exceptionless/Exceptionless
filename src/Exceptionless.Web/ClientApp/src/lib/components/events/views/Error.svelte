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
    import { H4 } from '$comp/typography';
    import type { IFilter } from '$comp/filters/filters';

    interface Props {
        event: PersistentEvent;
        changed: (filter: IFilter) => void;
    }

    let { event, changed }: Props = $props();

    let errorData = $derived(getErrorData(event));
    let errorType = $derived(getErrorType(event));
    let stackTrace = $derived(getStackTrace(event));

    let code = $derived(event.data?.['@error']?.code);
    let message = $derived(getMessage(event));
    let modules = $derived(event.data?.['@error']?.modules || []);
    let submissionMethod = $derived(event.data?.['@submission_method']);
</script>

<Table.Root>
    <Table.Body>
        <Table.Row class="group">
            <Table.Head class="w-40 whitespace-nowrap">Error Type</Table.Head>
            <Table.Cell class="w-4 pr-0 opacity-0 group-hover:opacity-100"><ClickableStringFilter term="error.type" value={errorType} {changed} /></Table.Cell>
            <Table.Cell>{errorType}</Table.Cell>
        </Table.Row>
        {#if message}
            <Table.Row class="group">
                <Table.Head class="w-40 whitespace-nowrap">Message</Table.Head>
                <Table.Cell class="w-4 pr-0 opacity-0 group-hover:opacity-100"
                    ><ClickableStringFilter term="error.message" value={message} {changed} /></Table.Cell
                >
                <Table.Cell>{message}</Table.Cell>
            </Table.Row>
        {/if}
        {#if code}
            <Table.Row class="group">
                <Table.Head class="w-40 whitespace-nowrap">Code</Table.Head>
                <Table.Cell class="w-4 pr-0 opacity-0 group-hover:opacity-100"><ClickableVersionFilter term="error.code" value={code} {changed} /></Table.Cell>
                <Table.Cell>{code}</Table.Cell>
            </Table.Row>
        {/if}
        {#if submissionMethod}
            <Table.Row>
                <Table.Head class="w-40 whitespace-nowrap">Submission Method</Table.Head>
                <Table.Cell class="w-4 pr-0"></Table.Cell>
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
