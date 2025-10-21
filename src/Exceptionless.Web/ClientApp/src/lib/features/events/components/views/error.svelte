<script lang="ts">
    import type { IFilter } from '$comp/faceted-filter';

    import CopyToClipboardButton from '$comp/copy-to-clipboard-button.svelte';
    import { H3 } from '$comp/typography';
    import * as Table from '$comp/ui/table';
    import * as EventsFacetedFilter from '$features/events/components/filters';
    import { getErrorData, getErrorType, getMessage, getStackTrace } from '$features/events/persistent-event';

    import type { PersistentEvent } from '../../models/index';

    import ExtendedDataItem from '../extended-data-item.svelte';
    import SimpleStackTrace from '../simple-stack-trace/simple-stack-trace.svelte';
    import StackTrace from '../stack-trace/stack-trace.svelte';

    interface Props {
        event: PersistentEvent;
        filterChanged: (filter: IFilter) => void;
    }

    let { event, filterChanged }: Props = $props();

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
            <Table.Head class="w-40 font-semibold whitespace-nowrap">Error Type</Table.Head>
            <Table.Cell class="w-4 pr-0"><EventsFacetedFilter.StringTrigger changed={filterChanged} term="error.type" value={errorType} /></Table.Cell>
            <Table.Cell>{errorType}</Table.Cell>
        </Table.Row>
        {#if message}
            <Table.Row class="group">
                <Table.Head class="w-40 font-semibold whitespace-nowrap">Message</Table.Head>
                <Table.Cell class="w-4 pr-0"><EventsFacetedFilter.StringTrigger changed={filterChanged} term="error.message" value={message} /></Table.Cell>
                <Table.Cell>{message}</Table.Cell>
            </Table.Row>
        {/if}
        {#if code}
            <Table.Row class="group">
                <Table.Head class="w-40 font-semibold whitespace-nowrap">Code</Table.Head>
                <Table.Cell class="w-4 pr-0"><EventsFacetedFilter.VersionTrigger changed={filterChanged} term="error.code" value={code} /></Table.Cell>
                <Table.Cell>{code}</Table.Cell>
            </Table.Row>
        {/if}
        {#if submissionMethod}
            <Table.Row>
                <Table.Head class="w-40 font-semibold whitespace-nowrap">Submission Method</Table.Head>
                <Table.Cell class="w-4 pr-0"></Table.Cell>
                <Table.Cell>{submissionMethod}</Table.Cell>
            </Table.Row>
        {/if}</Table.Body
    >
</Table.Root>

<div class="mt-4 mb-2 flex justify-between">
    <H3>Stack Trace</H3>
    <div class="flex justify-end">
        <CopyToClipboardButton title="Copy Stack Trace to Clipboard" value={stackTrace}></CopyToClipboardButton>
    </div>
</div>
<div class="mt-2 mb-4 overflow-auto text-xs">
    {#if event.data?.['@error']}
        <StackTrace error={event.data['@error']} />
    {:else if event.data?.['@simple_error']}
        <SimpleStackTrace error={event.data?.['@simple_error']} />
    {/if}
</div>

{#if errorData.length}
    <H3 class="mt-2 mb-2">Additional Data</H3>
    {#each errorData as ed, index (index)}
        <div class="mt-2">
            <ExtendedDataItem canPromote={false} data={ed.data} title={ed.type}></ExtendedDataItem>
        </div>
    {/each}
{/if}

{#if modules.length}
    <div class="mt-4 mb-2 flex items-center justify-between">
        <H3>Modules</H3>
    </div>
    <Table.Root>
        <Table.Header>
            <Table.Row>
                <Table.Head>Name</Table.Head>
                <Table.Head>Version</Table.Head>
            </Table.Row>
        </Table.Header>
        <Table.Body>
            {#each modules as module (module.module_id)}
                <Table.Row>
                    <Table.Cell class="whitespace-nowrap">{module.name}</Table.Cell>
                    <Table.Cell>{module.version}</Table.Cell>
                </Table.Row>
            {/each}</Table.Body
        >
    </Table.Root>
{/if}
