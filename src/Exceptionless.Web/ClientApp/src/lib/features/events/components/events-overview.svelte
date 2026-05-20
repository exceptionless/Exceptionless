<script lang="ts">
    import type { IFilter } from '$comp/faceted-filter';
    import type { ViewProject } from '$features/projects/models';
    import type { ProblemDetails } from '@exceptionless/fetchclient';

    import DateTime from '$comp/formatters/date-time.svelte';
    import TimeAgo from '$comp/formatters/time-ago.svelte';
    import { Button } from '$comp/ui/button';
    import { Skeleton } from '$comp/ui/skeleton';
    import * as Table from '$comp/ui/table';
    import * as Tabs from '$comp/ui/tabs';
    import { getEventQuery } from '$features/events/api.svelte';
    import * as EventsFacetedFilter from '$features/events/components/filters';
    import { getExtendedDataItems, hasErrorOrSimpleError } from '$features/events/persistent-event';
    import { getOrganizationQuery } from '$features/organizations/api.svelte';
    import { getProjectQuery } from '$features/projects/api.svelte';
    import StackCard from '$features/stacks/components/stack-card.svelte';
    import ChevronLeft from '@lucide/svelte/icons/chevron-left';
    import ChevronRight from '@lucide/svelte/icons/chevron-right';
    import { onMount, tick } from 'svelte';

    import type { PersistentEvent } from '../models/index';

    import { getSessionId } from '../utils';
    import Environment from './views/environment.svelte';
    import Error from './views/error.svelte';
    import ExtendedData from './views/extended-data.svelte';
    import Overview from './views/overview.svelte';
    import PromotedExtendedData from './views/promoted-extended-data.svelte';
    import Request from './views/request.svelte';
    import SessionEvents from './views/session-events.svelte';
    import TraceLog from './views/trace-log.svelte';

    interface Props {
        filterChanged: (filter: IFilter) => void;
        handleError: (problem: ProblemDetails) => void;
        id: string;
    }

    let { filterChanged, handleError, id }: Props = $props();

    function getTabs(event?: null | PersistentEvent, project?: ViewProject): TabType[] {
        if (!event) {
            return [];
        }

        const tabs: TabType[] = ['Overview'];
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

        if (getSessionId(event)) {
            tabs.push('Session Events');
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

    const eventQuery = getEventQuery({
        route: {
            get id() {
                return id;
            }
        }
    });

    const projectQuery = getProjectQuery({
        route: {
            get id() {
                return eventQuery.data?.project_id;
            }
        }
    });

    const organizationQuery = getOrganizationQuery({
        route: {
            get id() {
                return eventQuery.data?.organization_id;
            }
        }
    });

    const hasPremiumFeatures = $derived(!organizationQuery.isSuccess || !!organizationQuery.data?.has_premium_features);

    type TabType = 'Environment' | 'Exception' | 'Extended Data' | 'Overview' | 'Request' | 'Trace Log' | string;

    let activeTab = $state<TabType>('Overview');
    let tabs = $derived<TabType[]>(getTabs(eventQuery.data, projectQuery.data));
    let tabsListRef = $state<HTMLElement | null>(null);
    let canScrollTabsLeft = $state(false);
    let canScrollTabsRight = $state(false);

    function updateTabsOverflow(): void {
        if (!tabsListRef) {
            canScrollTabsLeft = false;
            canScrollTabsRight = false;
            return;
        }

        const maxScrollLeft = tabsListRef.scrollWidth - tabsListRef.clientWidth;
        canScrollTabsLeft = tabsListRef.scrollLeft > 1;
        canScrollTabsRight = tabsListRef.scrollLeft < maxScrollLeft - 1;
    }

    function scrollTabs(direction: 'left' | 'right'): void {
        if (!tabsListRef) {
            return;
        }

        tabsListRef.scrollBy({ behavior: 'smooth', left: direction === 'left' ? -tabsListRef.clientWidth / 2 : tabsListRef.clientWidth / 2 });
    }

    function onPromoted(title: string): void {
        activeTab = title;
    }

    function onDemoted(): void {
        activeTab = 'Extended Data';
    }

    $effect(() => {
        if (projectQuery.isError) {
            handleError(projectQuery.error);
        }

        if (eventQuery.isError) {
            handleError(eventQuery.error);
        }
    });

    $effect(() => {
        tabs.length;
        void tick().then(updateTabsOverflow);
    });

    onMount(() => {
        updateTabsOverflow();

        const resizeObserver = new ResizeObserver(updateTabsOverflow);
        if (tabsListRef) {
            resizeObserver.observe(tabsListRef);
        }

        window.addEventListener('resize', updateTabsOverflow);

        return () => {
            resizeObserver.disconnect();
            window.removeEventListener('resize', updateTabsOverflow);
        };
    });
</script>

<StackCard {filterChanged} id={eventQuery.data?.stack_id}></StackCard>

<Table.Root class="mt-4">
    <Table.Body>
        <Table.Row class="group">
            {#if eventQuery.isSuccess}
                <Table.Head class="w-40 font-semibold whitespace-nowrap">Occurred On</Table.Head>
                <Table.Cell class="w-4 pr-0"></Table.Cell>
                <Table.Cell class="flex items-center"
                    ><DateTime value={eventQuery.data.date}></DateTime> (<TimeAgo value={eventQuery.data.date}></TimeAgo>)</Table.Cell
                >
            {:else}
                <Table.Head class="w-40 font-semibold whitespace-nowrap"><Skeleton class="h-6 w-full rounded-full" /></Table.Head>
                <Table.Cell class="w-4 pr-0"></Table.Cell>
                <Table.Cell class="flex items-center"><Skeleton class="h-6 w-full rounded-full" /></Table.Cell>{/if}
        </Table.Row>
        <Table.Row class="group">
            {#if projectQuery.isSuccess}
                <Table.Head class="w-40 font-semibold whitespace-nowrap">Project</Table.Head>
                <Table.Cell class="w-4 pr-0"
                    ><EventsFacetedFilter.ProjectTrigger changed={filterChanged} class="mr-0" value={[projectQuery.data.id!]} /></Table.Cell
                >
                <Table.Cell>{projectQuery.data.name}</Table.Cell>
            {:else}
                <Table.Head class="w-40 font-semibold whitespace-nowrap"><Skeleton class="h-6 w-full rounded-full" /></Table.Head>
                <Table.Cell class="w-4 pr-0"></Table.Cell>
                <Table.Cell class="flex items-center"><Skeleton class="h-6 w-full rounded-full" /></Table.Cell>
            {/if}
        </Table.Row>
    </Table.Body>
</Table.Root>

{#if eventQuery.isSuccess}
    <Tabs.Root class="mt-4 mb-4" value={activeTab}>
        <div class="relative">
            {#if canScrollTabsLeft}
                <Button
                    aria-label="Scroll tabs left"
                    class="bg-background/95 absolute top-1/2 left-0 z-10 -translate-y-1/2 shadow-sm"
                    onclick={() => scrollTabs('left')}
                    size="icon-sm"
                    variant="outline"
                >
                    <ChevronLeft class="size-4" />
                </Button>
            {/if}
            <Tabs.List bind:ref={tabsListRef} class="no-scrollbar w-full justify-normal overflow-x-auto overflow-y-hidden scroll-smooth" onscroll={updateTabsOverflow}>
                {#each tabs as tab (tab)}
                    <Tabs.Trigger class="flex-none px-3" value={tab}>{tab}</Tabs.Trigger>
                {/each}
            </Tabs.List>
            {#if canScrollTabsRight}
                <Button
                    aria-label="Scroll tabs right"
                    class="bg-background/95 absolute top-1/2 right-0 z-10 -translate-y-1/2 shadow-sm"
                    onclick={() => scrollTabs('right')}
                    size="icon-sm"
                    variant="outline"
                >
                    <ChevronRight class="size-4" />
                </Button>
            {/if}
        </div>

        {#each tabs as tab (tab)}
            <Tabs.Content value={tab}>
                {#if tab === 'Overview'}
                    <Overview {filterChanged} event={eventQuery.data}></Overview>
                {:else if tab === 'Exception'}
                    <Error {filterChanged} event={eventQuery.data}></Error>
                {:else if tab === 'Environment'}
                    <Environment {filterChanged} event={eventQuery.data}></Environment>
                {:else if tab === 'Request'}
                    <Request {filterChanged} event={eventQuery.data}></Request>
                {:else if tab === 'Trace Log'}
                    <TraceLog logs={eventQuery.data.data?.['@trace']}></TraceLog>
                {:else if tab === 'Session Events'}
                    <SessionEvents event={eventQuery.data} {hasPremiumFeatures}></SessionEvents>
                {:else if tab === 'Extended Data'}
                    <ExtendedData event={eventQuery.data} project={projectQuery.data} promoted={onPromoted}></ExtendedData>
                {:else}
                    <PromotedExtendedData demoted={onDemoted} event={eventQuery.data} title={tab + ''}></PromotedExtendedData>
                {/if}
            </Tabs.Content>
        {/each}
    </Tabs.Root>
{:else}
    <Skeleton class="mt-4 h-7.5 w-full rounded-full" />
    <Table.Root class="mt-4">
        <Table.Body>
            {#each { length: 5 } as name, index (`${name}-${index}`)}
                <Table.Row class="group">
                    <Table.Head class="w-40 font-semibold whitespace-nowrap"><Skeleton class="h-6 w-full rounded-full" /></Table.Head>
                    <Table.Cell class="w-4 pr-0"></Table.Cell>
                    <Table.Cell class="flex items-center"><Skeleton class="h-6 w-full rounded-full" /></Table.Cell>
                </Table.Row>
            {/each}
        </Table.Body>
    </Table.Root>
{/if}
