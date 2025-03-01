<script lang="ts">
    import type { IFilter } from '$comp/faceted-filter';
    import type { ViewProject } from '$features/projects/models';

    import DateTime from '$comp/formatters/date-time.svelte';
    import TimeAgo from '$comp/formatters/time-ago.svelte';
    import { Skeleton } from '$comp/ui/skeleton';
    import * as Table from '$comp/ui/table';
    import * as Tabs from '$comp/ui/tabs';
    import { getEventQuery } from '$features/events/api.svelte';
    import * as EventsFacetedFilter from '$features/events/components/filters';
    import { getExtendedDataItems, hasErrorOrSimpleError } from '$features/events/persistent-event';
    import { getProjectQuery } from '$features/projects/api.svelte';
    import StackCard from '$features/stacks/components/stack-card.svelte';

    import type { PersistentEvent } from '../models/index';

    import Environment from './views/environment.svelte';
    import Error from './views/error.svelte';
    import ExtendedData from './views/extended-data.svelte';
    import Overview from './views/overview.svelte';
    import PromotedExtendedData from './views/promoted-extended-data.svelte';
    import Request from './views/request.svelte';
    import TraceLog from './views/trace-log.svelte';

    interface Props {
        changed: (filter: IFilter) => void;
        close: () => void;
        id: string;
    }

    let { changed, close, id }: Props = $props();

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

    const eventResponse = getEventQuery({
        route: {
            get id() {
                return id;
            }
        }
    });

    const projectResponse = getProjectQuery({
        route: {
            get id() {
                return eventResponse.data?.project_id;
            }
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

    $effect(() => {
        if (eventResponse.isError) {
            close();
        }
    });
</script>

<StackCard {changed} id={eventResponse.data?.stack_id}></StackCard>

<Table.Root class="mt-4">
    <Table.Body>
        <Table.Row class="group">
            {#if eventResponse.isSuccess}
                <Table.Head class="w-40 font-semibold whitespace-nowrap">Occurred On</Table.Head>
                <Table.Cell class="w-4 pr-0"></Table.Cell>
                <Table.Cell class="flex items-center"
                    ><DateTime value={eventResponse.data.date}></DateTime> (<TimeAgo value={eventResponse.data.date}></TimeAgo>)</Table.Cell
                >
            {:else}
                <Table.Head class="w-40 font-semibold whitespace-nowrap"><Skeleton class="h-[24px] w-full rounded-full" /></Table.Head>
                <Table.Cell class="w-4 pr-0"></Table.Cell>
                <Table.Cell class="flex items-center"><Skeleton class="h-[24px] w-full rounded-full" /></Table.Cell>{/if}
        </Table.Row>
        <Table.Row class="group">
            {#if projectResponse.isSuccess}
                <Table.Head class="w-40 font-semibold whitespace-nowrap">Project</Table.Head>
                <Table.Cell class="w-4 pr-0 opacity-0 group-hover:opacity-100"
                    ><EventsFacetedFilter.ProjectTrigger {changed} class="mr-0" value={[projectResponse.data.id!]} /></Table.Cell
                >
                <Table.Cell>{projectResponse.data.name}</Table.Cell>
            {:else}
                <Table.Head class="w-40 font-semibold whitespace-nowrap"><Skeleton class="h-[24px] w-full rounded-full" /></Table.Head>
                <Table.Cell class="w-4 pr-0"></Table.Cell>
                <Table.Cell class="flex items-center"><Skeleton class="h-[24px] w-full rounded-full" /></Table.Cell>
            {/if}
        </Table.Row>
    </Table.Body>
</Table.Root>

{#if eventResponse.isSuccess}
    <Tabs.Root class="mt-4 mb-4" value={activeTab}>
        <Tabs.List class="w-full justify-normal overflow-scroll">
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
    <Skeleton class="mt-4 h-[30px] w-full rounded-full" />
    <Table.Root class="mt-4">
        <Table.Body>
            {#each { length: 5 } as name, index (`${name}-${index}`)}
                <Table.Row class="group">
                    <Table.Head class="w-40 font-semibold whitespace-nowrap"><Skeleton class="h-[24px] w-full rounded-full" /></Table.Head>
                    <Table.Cell class="w-4 pr-0"></Table.Cell>
                    <Table.Cell class="flex items-center"><Skeleton class="h-[24px] w-full rounded-full" /></Table.Cell>
                </Table.Row>
            {/each}
        </Table.Body>
    </Table.Root>
{/if}
