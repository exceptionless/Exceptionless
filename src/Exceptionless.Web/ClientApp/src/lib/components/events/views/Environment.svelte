<script lang="ts">
    import type { PersistentEvent } from '$lib/models/api';
    import * as Table from '$comp/ui/table';
    import ClickableStringFilter from '$comp/filters/ClickableStringFilter.svelte';
    import ExtendedDataItem from '../ExtendedDataItem.svelte';
    import Bytes from '$comp/formatters/Bytes.svelte';
    import Number from '$comp/formatters/Number.svelte';
    import type { IFilter } from '$comp/filters/filters.svelte';

    interface Props {
        event: PersistentEvent;
        changed: (filter: IFilter) => void;
    }

    let { event, changed }: Props = $props();
    let environment = $derived(event.data?.['@environment'] ?? {});
</script>

<Table.Root>
    <Table.Body>
        {#if environment.machine_name}
            <Table.Row class="group">
                <Table.Head class="w-40 whitespace-nowrap">Machine Name</Table.Head>
                <Table.Cell class="w-4 pr-0 opacity-0 group-hover:opacity-100"
                    ><ClickableStringFilter term="machine" value={environment.machine_name} {changed} /></Table.Cell
                >
                <Table.Cell class="flex items-center">{environment.machine_name}</Table.Cell>
            </Table.Row>
        {/if}
        {#if environment.ip_address}
            <Table.Row class="group">
                <Table.Head class="w-40 whitespace-nowrap">IP Address</Table.Head>
                <Table.Cell class="w-4 pr-0 opacity-0 group-hover:opacity-100"
                    ><ClickableStringFilter term="ip" value={environment.ip_address} {changed} /></Table.Cell
                >
                <Table.Cell>{environment.ip_address}</Table.Cell>
            </Table.Row>
        {/if}
        {#if environment.processor_count}
            <Table.Row>
                <Table.Head class="w-40 whitespace-nowrap">Processor Count</Table.Head>
                <Table.Cell class="w-4 pr-0"></Table.Cell>
                <Table.Cell><Number value={environment.processor_count}></Number></Table.Cell>
            </Table.Row>
        {/if}
        {#if environment.total_physical_memory}
            <Table.Row>
                <Table.Head class="w-40 whitespace-nowrap">Total Memory</Table.Head>
                <Table.Cell class="w-4 pr-0"></Table.Cell>
                <Table.Cell><Bytes value={environment.total_physical_memory}></Bytes></Table.Cell>
            </Table.Row>
        {/if}
        {#if environment.available_physical_memory}
            <Table.Row>
                <Table.Head class="w-40 whitespace-nowrap">Available Memory</Table.Head>
                <Table.Cell class="w-4 pr-0"></Table.Cell>
                <Table.Cell><Bytes value={environment.available_physical_memory}></Bytes></Table.Cell>
            </Table.Row>
        {/if}
        {#if environment.process_memory_size}
            <Table.Row>
                <Table.Head class="w-40 whitespace-nowrap">Process Memory</Table.Head>
                <Table.Cell class="w-4 pr-0"></Table.Cell>
                <Table.Cell><Bytes value={environment.process_memory_size}></Bytes></Table.Cell>
            </Table.Row>
        {/if}
        {#if environment.o_s_name}
            <Table.Row class="group">
                <Table.Head class="w-40 whitespace-nowrap">OS Name</Table.Head>
                <Table.Cell class="w-4 pr-0 opacity-0 group-hover:opacity-100"
                    ><ClickableStringFilter term="os" value={environment.o_s_name} {changed} /></Table.Cell
                >
                <Table.Cell>{environment.o_s_name}</Table.Cell>
            </Table.Row>
        {/if}
        {#if environment.o_s_version}
            <Table.Row class="group">
                <Table.Head class="w-40 whitespace-nowrap">OS Version</Table.Head>
                <Table.Cell class="w-4 pr-0 opacity-0 group-hover:opacity-100"
                    ><ClickableStringFilter term="os.version" value={environment.o_s_version} {changed} /></Table.Cell
                >
                <Table.Cell class="flex items-center">{environment.o_s_version}</Table.Cell>
            </Table.Row>
        {/if}
        {#if environment.architecture}
            <Table.Row class="group">
                <Table.Head class="w-40 whitespace-nowrap">Architecture</Table.Head>
                <Table.Cell class="w-4 pr-0 opacity-0 group-hover:opacity-100"
                    ><ClickableStringFilter term="architecture" value={environment.architecture} {changed} /></Table.Cell
                >
                <Table.Cell class="flex items-center">{environment.architecture}</Table.Cell>
            </Table.Row>
        {/if}
        {#if environment.runtime_version}
            <Table.Row>
                <Table.Head class="w-40 whitespace-nowrap">Runtime Version</Table.Head>
                <Table.Cell class="w-4 pr-0"></Table.Cell>
                <Table.Cell>{environment.runtime_version}</Table.Cell>
            </Table.Row>
        {/if}
        {#if environment.process_id}
            <Table.Row>
                <Table.Head class="w-40 whitespace-nowrap">Process ID</Table.Head>
                <Table.Cell class="w-4 pr-0"></Table.Cell>
                <Table.Cell>{environment.process_id}</Table.Cell>
            </Table.Row>
        {/if}
        {#if environment.process_name}
            <Table.Row>
                <Table.Head class="w-40 whitespace-nowrap">Process Name</Table.Head>
                <Table.Cell class="w-4 pr-0"></Table.Cell>
                <Table.Cell>{environment.process_name}</Table.Cell>
            </Table.Row>
        {/if}
        {#if environment.command_line}
            <Table.Row>
                <Table.Head class="w-40 whitespace-nowrap">Command Line</Table.Head>
                <Table.Cell class="w-4 pr-0"></Table.Cell>
                <Table.Cell><span class="line-clamp-2 inline">{environment.command_line}</span></Table.Cell>
            </Table.Row>
        {/if}
    </Table.Body>
</Table.Root>

{#if environment.data}
    <div class="mt-2">
        <ExtendedDataItem canPromote={false} title="Additional Data" data={environment.data}></ExtendedDataItem>
    </div>
{/if}
