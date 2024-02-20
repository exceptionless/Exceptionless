<script lang="ts">
    import { writable, type Writable } from 'svelte/store';

    import ErrorMessage from '$comp/ErrorMessage.svelte';
    import { getExtendedDataItems, hasErrorOrSimpleError } from '$lib/helpers/persistent-event';
    import type { PersistentEvent, ViewProject } from '$lib/models/api';
    import Error from './views/Error.svelte';
    import Overview from './views/Overview.svelte';
    import Environment from './views/Environment.svelte';
    import Request from './views/Request.svelte';
    import TraceLog from './views/TraceLog.svelte';
    import ExtendedData from './views/ExtendedData.svelte';
    import { getEventByIdQuery } from '$api/eventsApi';
    import DateTime from '$comp/formatters/DateTime.svelte';
    import ClickableDateFilter from '$comp/filters/ClickableDateFilter.svelte';
    import TimeAgo from '$comp/formatters/TimeAgo.svelte';
    import { getProjectByIdQuery } from '$api/projectsApi';
    import { getStackByIdQuery } from '$api/stacksApi';
    import ClickableStringFilter from '$comp/filters/ClickableStringFilter.svelte';
    import PromotedExtendedData from './views/PromotedExtendedData.svelte';
    import * as Table from '$comp/ui/table';
    import * as Tabs from '$comp/ui/tabs';
    import P from '$comp/typography/P.svelte';

    export let id: string;

    type TabType = 'Overview' | 'Exception' | 'Environment' | 'Request' | 'Trace Log' | 'Extended Data' | string;

    let activeTab: TabType = 'Overview';
    const tabs: Writable<TabType[]> = writable([]);
    tabs.subscribe((items) => {
        if (!items) {
            activeTab = 'Overview';
        }

        if (!items.includes(activeTab)) {
            activeTab = items[0];
        }
    });

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

        if (project) {
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
        }

        return tabs;
    }

    const projectId = writable<string | null>(null);
    const projectResponse = getProjectByIdQuery(projectId);

    const stackId = writable<string | null>(null);
    const stackResponse = getStackByIdQuery(stackId);

    const eventResponse = getEventByIdQuery(id);
    eventResponse.subscribe((response) => {
        projectId.set(response.data?.project_id ?? null);
        stackId.set(response.data?.stack_id ?? null);
        tabs.set(getTabs(response.data, $projectResponse.data));
    });

    projectResponse.subscribe((response) => {
        tabs.set(getTabs($eventResponse.data, response.data));
    });

    function onPromoted({ detail }: CustomEvent<string>): void {
        tabs.update((items) => {
            items.splice(items.length - 1, 0, detail);
            return items;
        });
        activeTab = detail;
    }

    function onDemoted({ detail }: CustomEvent<string>): void {
        tabs.update((items) => {
            items.splice(items.indexOf(detail), 1);

            if (!items.includes('Extended Data')) {
                items.push('Extended Data');
            }

            return items;
        });
        activeTab = 'Extended Data';
    }
</script>

{#if $eventResponse.isLoading}
    <P>Loading...</P>
{:else if $eventResponse.isSuccess}
    <Table.Root class="mt-4">
        <Table.Body>
            <Table.Row>
                <Table.Head class="whitespace-nowrap">Occurred On</Table.Head>
                <Table.Cell
                    ><ClickableDateFilter term="date" value={$eventResponse.data.date}
                        ><DateTime value={$eventResponse.data.date}></DateTime> (<TimeAgo value={$eventResponse.data.date}></TimeAgo>)</ClickableDateFilter
                    ></Table.Cell
                >
            </Table.Row>
            {#if $projectResponse.data}
                <Table.Row>
                    <Table.Head class="whitespace-nowrap">Project</Table.Head>
                    <Table.Cell
                        ><ClickableStringFilter term="project" value={$projectResponse.data.id}>{$projectResponse.data.name}</ClickableStringFilter></Table.Cell
                    >
                </Table.Row>
            {/if}
            {#if $stackResponse.data}
                <Table.Row>
                    <Table.Head class="whitespace-nowrap">Stack</Table.Head>
                    <Table.Cell
                        ><ClickableStringFilter term="stack" value={$stackResponse.data.id}>{$stackResponse.data.title}</ClickableStringFilter></Table.Cell
                    >
                </Table.Row>
            {/if}
        </Table.Body>
    </Table.Root>

    <Tabs.Root value={activeTab} class="mb-4 mt-4">
        <Tabs.List class="mb-4 w-full justify-normal">
            {#each $tabs as tab (tab)}
                <Tabs.Trigger value={tab}>{tab}</Tabs.Trigger>
            {/each}
        </Tabs.List>

        {#each $tabs as tab (tab)}
            <Tabs.Content value={tab}>
                {#if tab === 'Overview'}
                    <Overview event={$eventResponse.data}></Overview>
                {:else if tab === 'Exception'}
                    <Error event={$eventResponse.data}></Error>
                {:else if tab === 'Environment'}
                    <Environment event={$eventResponse.data}></Environment>
                {:else if tab === 'Request'}
                    <Request event={$eventResponse.data}></Request>
                {:else if tab === 'Trace Log'}
                    <TraceLog logs={$eventResponse.data.data?.['@trace']}></TraceLog>
                {:else if tab === 'Extended Data'}
                    <ExtendedData event={$eventResponse.data} project={$projectResponse.data} on:promoted={onPromoted}></ExtendedData>
                {:else}
                    <PromotedExtendedData title={tab + ''} event={$eventResponse.data} on:demoted={onDemoted}></PromotedExtendedData>
                {/if}
            </Tabs.Content>
        {/each}
    </Tabs.Root>
{:else}
    <ErrorMessage message={$eventResponse.error?.errors.general}></ErrorMessage>
{/if}
