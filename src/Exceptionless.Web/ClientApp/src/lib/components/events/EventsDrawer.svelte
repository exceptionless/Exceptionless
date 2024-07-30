<script lang="ts">
    import ErrorMessage from '$comp/ErrorMessage.svelte';
    import { getExtendedDataItems, hasErrorOrSimpleError } from '$lib/helpers/persistent-event';
    import type { PersistentEvent, ViewProject } from '$lib/models/api';
    import Error from './views/Error.svelte';
    import Overview from './views/Overview.svelte';
    import Environment from './views/Environment.svelte';
    import Request from './views/Request.svelte';
    import TraceLog from './views/TraceLog.svelte';
    import ExtendedData from './views/ExtendedData.svelte';
    import { getEventByIdQuery } from '$api/eventsApi.svelte';
    import DateTime from '$comp/formatters/DateTime.svelte';
    import TimeAgo from '$comp/formatters/TimeAgo.svelte';
    import { getProjectByIdQuery } from '$api/projectsApi.svelte';
    import { getStackByIdQuery } from '$api/stacksApi.svelte';
    import PromotedExtendedData from './views/PromotedExtendedData.svelte';
    import * as Table from '$comp/ui/table';
    import * as Tabs from '$comp/ui/tabs';
    import { P } from '$comp/typography';
    import ClickableStringFilter from '$comp/filters/ClickableStringFilter.svelte';
    import ClickableProjectFilter from '$comp/filters/ClickableProjectFilter.svelte';
    import type { IFilter } from '$comp/filters/filters.svelte';

    interface Props {
        id: string;
        changed: (filter: IFilter) => void;
    }

    let { id, changed }: Props = $props();

    function getTabs(event?: PersistentEvent | null, project?: ViewProject): TabType[] {
        if (!event) {
            return [];
        }

        const tabs = ['Overview'];
        if (hasErrorOrSimpleError(event)) {
            tabs.push('Exception');
        }

        if (event.data?.['@environment']) {
            tabs.push('Environment');
        }

        if (event.data?.['@request']) {
            tabs.push('Request');
        }

        if (event.data?.['@trace']) {
            tabs.push('Trace Log');
        }

        if (!project) {
            return tabs;
        }

        const extendedDataItems = getExtendedDataItems(event, project);
        let hasExtendedData = false;

        for (const item of extendedDataItems) {
            if (item.promoted) {
                tabs.push(item.title);
            } else {
                hasExtendedData = true;
            }
        }

        if (hasExtendedData) {
            tabs.push('Extended Data');
        }

        return tabs;
    }

    let eventResponse = getEventByIdQuery({
        get id() {
            return id;
        }
    });

    let projectResponse = getProjectByIdQuery({
        get id() {
            return eventResponse.data?.project_id;
        }
    });

    let stackResponse = getStackByIdQuery({
        get id() {
            return eventResponse.data?.stack_id;
        }
    });

    type TabType = 'Overview' | 'Exception' | 'Environment' | 'Request' | 'Trace Log' | 'Extended Data' | string;

    let activeTab = $state<TabType>('Overview');
    let tabs = $derived<TabType[]>(getTabs(eventResponse.data, projectResponse.data));

    function onPromoted(title: string): void {
        activeTab = title;
    }

    function onDemoted(): void {
        activeTab = 'Extended Data';
    }
</script>

{#if eventResponse.isLoading}
    <P>Loading...</P>
{:else if eventResponse.isSuccess}
    <Table.Root class="mt-4">
        <Table.Body>
            <Table.Row class="group">
                <Table.Head class="w-40 whitespace-nowrap">Occurred On</Table.Head>
                <Table.Cell class="w-4 pr-0"></Table.Cell>
                <Table.Cell class="flex items-center"
                    ><DateTime value={eventResponse.data.date}></DateTime> (<TimeAgo value={eventResponse.data.date}></TimeAgo>)</Table.Cell
                >
            </Table.Row>
            {#if projectResponse.data}
                <Table.Row class="group">
                    <Table.Head class="w-40 whitespace-nowrap">Project</Table.Head>
                    <Table.Cell class="w-4 pr-0 opacity-0 group-hover:opacity-100"
                        ><ClickableProjectFilter
                            organization={projectResponse.data.organization_id!}
                            value={[projectResponse.data.id!]}
                            {changed}
                            class="mr-0"
                        /></Table.Cell
                    >
                    <Table.Cell>{projectResponse.data.name}</Table.Cell>
                </Table.Row>
            {/if}
            {#if stackResponse.data}
                <Table.Row class="group">
                    <Table.Head class="w-40 whitespace-nowrap">Stack</Table.Head>
                    <Table.Cell class="w-4 pr-0 opacity-0 group-hover:opacity-100"
                        ><ClickableStringFilter term="stack" value={stackResponse.data.id} {changed} class="mr-0" /></Table.Cell
                    >
                    <Table.Cell>{stackResponse.data.title}</Table.Cell>
                </Table.Row>
            {/if}
        </Table.Body>
    </Table.Root>

    <Tabs.Root value={activeTab} class="mb-4 mt-4">
        <Tabs.List class="mb-4 w-full justify-normal">
            {#each tabs as tab (tab)}
                <Tabs.Trigger value={tab}>{tab}</Tabs.Trigger>
            {/each}
        </Tabs.List>

        {#each tabs as tab (tab)}
            <Tabs.Content value={tab}>
                {#if tab === 'Overview'}
                    <Overview event={eventResponse.data} {changed}></Overview>
                {:else if tab === 'Exception'}
                    <Error event={eventResponse.data} {changed}></Error>
                {:else if tab === 'Environment'}
                    <Environment event={eventResponse.data} {changed}></Environment>
                {:else if tab === 'Request'}
                    <Request event={eventResponse.data} {changed}></Request>
                {:else if tab === 'Trace Log'}
                    <TraceLog logs={eventResponse.data.data?.['@trace']}></TraceLog>
                {:else if tab === 'Extended Data'}
                    <ExtendedData event={eventResponse.data} project={projectResponse.data} promoted={onPromoted}></ExtendedData>
                {:else}
                    <PromotedExtendedData title={tab + ''} event={eventResponse.data} demoted={onDemoted}></PromotedExtendedData>
                {/if}
            </Tabs.Content>
        {/each}
    </Tabs.Root>
{:else}
    <ErrorMessage message={eventResponse.error?.errors.general}></ErrorMessage>
{/if}
