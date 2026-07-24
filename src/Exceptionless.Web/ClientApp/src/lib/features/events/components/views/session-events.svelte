<script lang="ts">
    import type { PersistentEvent } from '$features/events/models';

    import { goto } from '$app/navigation';
    import { resolve } from '$app/paths';
    import TimeAgo from '$comp/formatters/time-ago.svelte';
    import * as Alert from '$comp/ui/alert';
    import { Button } from '$comp/ui/button';
    import { Skeleton } from '$comp/ui/skeleton';
    import * as Table from '$comp/ui/table';
    import { getSessionEventsQuery } from '$features/events/api.svelte';
    import { SessionFilter } from '$features/events/components/filters';
    import { buildFilterCacheKey, toFilter, updateFilterCache } from '$features/events/components/filters/helpers.svelte';
    import Summary from '$features/events/components/summary/summary.svelte';
    import { getSessionId } from '$features/events/utils/index';
    import { organization } from '$features/organizations/context.svelte';
    import EventsIcon from '@lucide/svelte/icons/calendar-days';
    import InfoIcon from '@lucide/svelte/icons/info';

    import SessionEventDuration from '../session-event-duration.svelte';

    interface Props {
        event: PersistentEvent;
        hasPremiumFeatures?: boolean;
        onSessionFilter?: () => void;
        time?: string;
    }

    let { event, hasPremiumFeatures = false, onSessionFilter, time }: Props = $props();

    const sessionId = $derived(getSessionId(event));
    const isSessionStart = $derived(event.type === 'session');
    const eventsPath = $derived(resolve('/(app)/event'));
    const sessionEventsHref = $derived.by(() => {
        const filter = getSessionFilter();
        if (!filter) {
            return undefined;
        }

        const query = new URLSearchParams({ filter: filter.toFilter(), time: 'all' });
        return `${eventsPath}?${query.toString()}`;
    });

    const userInfo = $derived(event.data?.['@user']);
    const userIdentity = $derived(userInfo?.identity);
    const userName = $derived(userInfo?.name);

    const queryParams = $derived({
        filter: '-type:heartbeat' as const,
        limit: 10 as const,
        ...(time ? { time } : {})
    });

    const sessionEventsQuery = getSessionEventsQuery({
        get params() {
            return queryParams;
        },
        route: {
            get projectId() {
                return event.project_id;
            },
            get sessionId() {
                return hasPremiumFeatures ? sessionId : undefined;
            }
        }
    });

    function getEventHref(eventId: string): string {
        return resolve('/(app)/event/[eventId=objectid]', { eventId });
    }

    function getSessionFilter(): SessionFilter | undefined {
        return sessionId ? new SessionFilter(sessionId) : undefined;
    }

    function prepareSessionEventsFilter(): void {
        const filter = getSessionFilter();
        if (!filter) {
            return;
        }

        const filterQuery = toFilter([filter]);
        updateFilterCache(buildFilterCacheKey(organization.current, eventsPath, filterQuery), [filter]);
    }

    function handleSessionFilterClick(): void {
        prepareSessionEventsFilter();
        onSessionFilter?.();
    }

    async function openSessionEvent(eventId: string): Promise<void> {
        await goto(getEventHref(eventId));
    }

    function handleSessionEventKeydown(keyboardEvent: KeyboardEvent, eventId: string): void {
        if (keyboardEvent.key !== 'Enter' && keyboardEvent.key !== ' ') {
            return;
        }

        keyboardEvent.preventDefault();
        void openSessionEvent(eventId);
    }
</script>

{#if !hasPremiumFeatures}
    <Alert.Root variant="destructive" class="mb-4">
        <InfoIcon class="size-4" />
        <Alert.Title>Premium Feature</Alert.Title>
        <Alert.Description>Sessions are a premium feature. Upgrade your plan to view session events.</Alert.Description>
    </Alert.Root>
{/if}

<div class="relative pr-10" class:opacity-60={!hasPremiumFeatures}>
    {#if isSessionStart}
        <Table.Root class="mb-4">
            <Table.Body>
                <Table.Row>
                    <Table.Head class="w-40 font-semibold whitespace-nowrap">Duration</Table.Head>
                    <Table.Cell class="w-4 pr-0"></Table.Cell>
                    <Table.Cell>
                        <SessionEventDuration {event} />
                    </Table.Cell>
                </Table.Row>
                {#if userIdentity}
                    <Table.Row>
                        <Table.Head class="w-40 font-semibold whitespace-nowrap">User Identity</Table.Head>
                        <Table.Cell class="w-4 pr-0"></Table.Cell>
                        <Table.Cell>{userIdentity}</Table.Cell>
                    </Table.Row>
                {/if}
                {#if userName}
                    <Table.Row>
                        <Table.Head class="w-40 font-semibold whitespace-nowrap">User Name</Table.Head>
                        <Table.Cell class="w-4 pr-0"></Table.Cell>
                        <Table.Cell>{userName}</Table.Cell>
                    </Table.Row>
                {/if}
            </Table.Body>
        </Table.Root>
    {/if}

    <div class="absolute top-0 right-0 z-10">
        <Button
            aria-label="Open events filtered to this session"
            disabled={!sessionEventsHref}
            href={sessionEventsHref}
            onclick={handleSessionFilterClick}
            size="icon-sm"
            title="Open events filtered to this session"
            variant="outline"
        >
            <EventsIcon class="size-4" />
        </Button>
    </div>

    {#if sessionEventsQuery.isPending}
        <div class="space-y-2">
            {#each Array.from({ length: 5 }, (_, i) => i) as i (i)}
                <Skeleton class="h-10 w-full" />
            {/each}
        </div>
    {:else if sessionEventsQuery.isError}
        <Alert.Root variant="destructive">
            <Alert.Title>Error loading session events</Alert.Title>
            <Alert.Description>{sessionEventsQuery.error?.message ?? 'Unknown error'}</Alert.Description>
        </Alert.Root>
    {:else if (sessionEventsQuery.data ?? []).length > 0}
        <Table.Root>
            <Table.Header>
                <Table.Row>
                    <Table.Head>Summary</Table.Head>
                    <Table.Head class="w-32">Session Time</Table.Head>
                </Table.Row>
            </Table.Header>
            <Table.Body>
                {#each sessionEventsQuery.data ?? [] as sessionEvent (sessionEvent.id)}
                    <Table.Row
                        aria-label={`Open event ${sessionEvent.id}`}
                        class="hover:bg-muted/50 focus-visible:ring-ring/50 focus-visible:outline-ring cursor-pointer focus-visible:ring-[3px] focus-visible:outline-1"
                        onclick={() => openSessionEvent(sessionEvent.id)}
                        onkeydown={(keyboardEvent) => handleSessionEventKeydown(keyboardEvent, sessionEvent.id)}
                        role="link"
                        tabindex={0}
                        title="Open event details"
                    >
                        <Table.Cell>
                            <Summary summary={sessionEvent} showType={true} showStatus={false} />
                        </Table.Cell>
                        <Table.Cell>
                            <TimeAgo value={sessionEvent.date} />
                        </Table.Cell>
                    </Table.Row>
                {/each}
            </Table.Body>
        </Table.Root>
    {:else}
        <p class="text-muted-foreground">No session events found.</p>
    {/if}
</div>
