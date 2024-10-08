<script lang="ts">
    import type { IFilter } from '$comp/filters/filters.svelte';

    import ErrorMessage from '$comp/ErrorMessage.svelte';
    import ClickableProjectFilter from '$comp/filters/ClickableProjectFilter.svelte';
    import ClickableStringFilter from '$comp/filters/ClickableStringFilter.svelte';
    import DateTime from '$comp/formatters/DateTime.svelte';
    import TimeAgo from '$comp/formatters/TimeAgo.svelte';
    import { P } from '$comp/typography';
    import * as Table from '$comp/ui/table';
    import * as Tabs from '$comp/ui/tabs';
    import { getEventByIdQuery } from '$features/events/api.svelte';
    import { getExtendedDataItems, hasErrorOrSimpleError } from '$features/events/persistent-event';
    import { getProjectByIdQuery } from '$features/projects/api.svelte';
    import { getStackByIdQuery } from '$features/stacks/api.svelte';

    import type { PersistentEvent, ViewProject } from '../models/index';

    import Environment from './views/Environment.svelte';
    import Error from './views/Error.svelte';
    import ExtendedData from './views/ExtendedData.svelte';
    import Overview from './views/Overview.svelte';
    import PromotedExtendedData from './views/PromotedExtendedData.svelte';
    import Request from './views/Request.svelte';
    import TraceLog from './views/TraceLog.svelte';

    interface Props {
        changed: (filter: IFilter) => void;
        id: string;
    }

    let { changed, id }: Props = $props();

    function getTabs(event?: null | PersistentEvent, project?: ViewProject): TabType[] {
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

    type TabType = 'Environment' | 'Exception' | 'Extended Data' | 'Overview' | 'Request' | 'Trace Log' | string;

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
                            {changed}
                            class="mr-0"
                            organization={projectResponse.data.organization_id!}
                            value={[projectResponse.data.id!]}
                        /></Table.Cell
                    >
                    <Table.Cell>{projectResponse.data.name}</Table.Cell>
                </Table.Row>
            {/if}
            {#if stackResponse.data}
                <Table.Row class="group">
                    <Table.Head class="w-40 whitespace-nowrap">Stack</Table.Head>
                    <Table.Cell class="w-4 pr-0 opacity-0 group-hover:opacity-100"
                        ><ClickableStringFilter {changed} class="mr-0" term="stack" value={stackResponse.data.id} /></Table.Cell
                    >
                    <Table.Cell>{stackResponse.data.title}</Table.Cell>
                </Table.Row>
            {/if}
        </Table.Body>
    </Table.Root>

    <Tabs.Root class="mb-4 mt-4" value={activeTab}>
        <Tabs.List class="mb-4 w-full justify-normal">
            {#each tabs as tab (tab)}
                <Tabs.Trigger value={tab}>{tab}</Tabs.Trigger>
            {/each}
        </Tabs.List>

        {#each tabs as tab (tab)}
            <Tabs.Content value={tab}>
                {#if tab === 'Overview'}
                    <Overview {changed} event={eventResponse.data}></Overview>
                {:else if tab === 'Exception'}
                    <Error {changed} event={eventResponse.data}></Error>
                {:else if tab === 'Environment'}
                    <Environment {changed} event={eventResponse.data}></Environment>
                {:else if tab === 'Request'}
                    <Request {changed} event={eventResponse.data}></Request>
                {:else if tab === 'Trace Log'}
                    <TraceLog logs={eventResponse.data.data?.['@trace']}></TraceLog>
                {:else if tab === 'Extended Data'}
                    <ExtendedData event={eventResponse.data} project={projectResponse.data} promoted={onPromoted}></ExtendedData>
                {:else}
                    <PromotedExtendedData demoted={onDemoted} event={eventResponse.data} title={tab + ''}></PromotedExtendedData>
                {/if}
            </Tabs.Content>
        {/each}
    </Tabs.Root>
{:else}
    <ErrorMessage message={eventResponse.error?.errors.general}></ErrorMessage>
{/if}
