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
    import { getEventQuery, getStackEventsQuery } from '$features/events/api.svelte';
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
        onEventChange?: (eventId: string) => void;
        onSessionFilter?: () => void;
        showStackPager?: boolean;
    }

    let { filterChanged, handleError, id, onEventChange, onSessionFilter, showStackPager = false }: Props = $props();

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
            tabs.push('Session');
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

    const stackEventsQuery = getStackEventsQuery({
        params: {
            limit: 100,
            sort: '-date'
        },
        route: {
            get stackId() {
                return eventQuery.data?.stack_id;
            }
        }
    });

    const currentEventIndex = $derived(stackEventsQuery.data?.findIndex((event) => event.id === id) ?? -1);
    const previousEvent = $derived(currentEventIndex > 0 ? stackEventsQuery.data?.[currentEventIndex - 1] : null);
    const nextEvent = $derived(currentEventIndex >= 0 ? (stackEventsQuery.data?.[currentEventIndex + 1] ?? null) : null);

    function changeEvent(eventId: string | undefined): void {
        if (!eventId) {
            return;
        }

        onEventChange?.(eventId);
    }

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
    let areTabsScrollable = $state(false);
    let canScrollTabsLeft = $state(false);
    let canScrollTabsRight = $state(false);
    let shouldRoundLastTab = $state(false);
    let tabsListElement = $state<HTMLElement | null>(null);
    let tabs = $derived<TabType[]>(getTabs(eventQuery.data, projectQuery.data));

    function updateTabScrollState(): void {
        if (!tabsListElement) {
            areTabsScrollable = false;
            canScrollTabsLeft = false;
            canScrollTabsRight = false;
            shouldRoundLastTab = false;
            return;
        }

        const maxScrollLeft = tabsListElement.scrollWidth - tabsListElement.clientWidth;
        const lastTabElement = tabsListElement.querySelector('[data-slot="tabs-trigger"]:last-child');
        areTabsScrollable = maxScrollLeft > 1;
        canScrollTabsLeft = tabsListElement.scrollLeft > 1;
        canScrollTabsRight = tabsListElement.scrollLeft < maxScrollLeft - 1;
        shouldRoundLastTab = areTabsScrollable || (lastTabElement?.getBoundingClientRect().right ?? 0) >= tabsListElement.getBoundingClientRect().right - 1;
    }

    function scrollTabs(direction: -1 | 1): void {
        if (!tabsListElement) {
            return;
        }

        tabsListElement.scrollBy({
            behavior: 'smooth',
            left: direction * Math.max(tabsListElement.clientWidth * 0.75, 160)
        });
    }

    async function refreshTabScrollState(currentTabs: TabType[]): Promise<void> {
        await tick();
        if (currentTabs !== tabs) {
            return;
        }

        updateTabScrollState();
    }

    async function scrollActiveTabIntoView(currentTab: TabType): Promise<void> {
        await tick();
        if (currentTab !== activeTab) {
            return;
        }

        tabsListElement?.querySelector('[data-state="active"]')?.scrollIntoView({ block: 'nearest', inline: 'nearest' });
        updateTabScrollState();
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
        void refreshTabScrollState(tabs);
    });

    $effect(() => {
        void scrollActiveTabIntoView(activeTab);
    });

    onMount(() => {
        updateTabScrollState();
        window.addEventListener('resize', updateTabScrollState);

        return () => {
            window.removeEventListener('resize', updateTabScrollState);
        };
    });
</script>

<StackCard {filterChanged} id={eventQuery.data?.stack_id}></StackCard>

{#if showStackPager && eventQuery.data?.stack_id}
    <div class="mt-3 flex items-center justify-end gap-2">
        <Button type="button" variant="outline" size="sm" onclick={() => changeEvent(previousEvent?.id)} disabled={!previousEvent}
            ><ChevronLeft class="size-4" /> Previous Event</Button
        >
        <Button type="button" variant="outline" size="sm" onclick={() => changeEvent(nextEvent?.id)} disabled={!nextEvent}
            >Next Event <ChevronRight class="size-4" /></Button
        >
    </div>
{/if}

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
        <div class="flex min-w-0 items-center gap-1">
            {#if areTabsScrollable}
                <Button
                    aria-label="Scroll tabs left"
                    class="flex-none"
                    disabled={!canScrollTabsLeft}
                    onclick={() => scrollTabs(-1)}
                    size="icon-sm"
                    title="Scroll tabs left"
                    variant="ghost"
                >
                    <ChevronLeft />
                </Button>
            {/if}

            <Tabs.List
                bind:ref={tabsListElement}
                class="divide-border bg-background h-8 min-w-0 flex-1 justify-normal divide-x overflow-x-auto overflow-y-hidden rounded-lg border p-0 shadow-xs [scrollbar-width:none] [&::-webkit-scrollbar]:hidden"
                onscroll={updateTabScrollState}
            >
                {#each tabs as tab (tab)}
                    <Tabs.Trigger
                        class={[
                            'data-[state=active]:bg-muted flex-none rounded-none border-0 px-4 shadow-none first:rounded-l-lg data-[state=active]:shadow-none',
                            shouldRoundLastTab && 'last:rounded-r-lg'
                        ]}
                        value={tab}
                    >
                        {tab}
                    </Tabs.Trigger>
                {/each}
            </Tabs.List>

            {#if areTabsScrollable}
                <Button
                    aria-label="Scroll tabs right"
                    class="flex-none"
                    disabled={!canScrollTabsRight}
                    onclick={() => scrollTabs(1)}
                    size="icon-sm"
                    title="Scroll tabs right"
                    variant="ghost"
                >
                    <ChevronRight />
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
                {:else if tab === 'Session'}
                    <SessionEvents event={eventQuery.data} {hasPremiumFeatures} {onSessionFilter}></SessionEvents>
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
