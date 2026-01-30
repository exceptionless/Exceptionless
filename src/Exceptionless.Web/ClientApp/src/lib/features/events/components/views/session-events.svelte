<script lang="ts">
    import type { PersistentEvent } from '$features/events/models';

    import { resolve } from '$app/paths';
    import Duration from '$comp/formatters/duration.svelte';
    import TimeAgo from '$comp/formatters/time-ago.svelte';
    import { A } from '$comp/typography';
    import * as Alert from '$comp/ui/alert';
    import { Skeleton } from '$comp/ui/skeleton';
    import * as Table from '$comp/ui/table';
    import { getSessionEventsQuery } from '$features/sessions/api.svelte';
    import InfoIcon from '@lucide/svelte/icons/info';

    interface Props {
        event: PersistentEvent;
        hasPremiumFeatures?: boolean;
    }

    let { event, hasPremiumFeatures = true }: Props = $props();

    // Determine session ID from event
    // For session start events, use reference_id
    // For other events, check @ref:session in data
    const sessionId = $derived(() => {
        if (event.type === 'session') {
            return event.reference_id ?? undefined;
        }
        return event.data?.['@ref:session'] as string | undefined;
    });

    const isSessionStart = $derived(event.type === 'session');

    // Calculate session duration for session start events
    const sessionDuration = $derived(() => {
        if (!isSessionStart) return 0;
        const sessionEnd = event.data?.sessionend as string | undefined;
        if (sessionEnd) {
            // Duration is stored in value field (in seconds)
            return (event.data?.Value as number) ?? 0;
        }
        // Active session - calculate from now
        const startDate = new Date(event.date);
        return Math.floor((Date.now() - startDate.getTime()) / 1000);
    });

    const isActiveSession = $derived(isSessionStart && !event.data?.sessionend);

    // Query for session events
    const sessionEventsQuery = getSessionEventsQuery({
        params: {
            filter: '-type:heartbeat', // Exclude heartbeats like legacy
            limit: 10
        },
        route: {
            get sessionId() {
                return sessionId();
            }
        }
    });
</script>

{#if !hasPremiumFeatures}
    <Alert.Root variant="destructive" class="mb-4">
        <InfoIcon class="h-4 w-4" />
        <Alert.Title>Premium Feature</Alert.Title>
        <Alert.Description>
            Sessions are a premium feature. Upgrade your plan to view session events.
        </Alert.Description>
    </Alert.Root>
{/if}

<div class:opacity-60={!hasPremiumFeatures}>
    {#if isSessionStart}
        <Table.Root class="mb-4">
            <Table.Body>
                <Table.Row class="group">
                    <Table.Head class="w-40 font-semibold whitespace-nowrap">Occurred On</Table.Head>
                    <Table.Cell class="w-4 pr-0"></Table.Cell>
                    <Table.Cell class="flex items-center gap-2">
                        <TimeAgo value={event.date} />
                    </Table.Cell>
                </Table.Row>
                <Table.Row class="group">
                    <Table.Head class="w-40 font-semibold whitespace-nowrap">Duration</Table.Head>
                    <Table.Cell class="w-4 pr-0"></Table.Cell>
                    <Table.Cell class="flex items-center gap-2">
                        {#if isActiveSession}
                            <span class="inline-block h-2 w-2 rounded-full bg-green-500" title="Online"></span>
                        {/if}
                        <Duration value={sessionDuration() * 1000} />
                        {#if event.data?.sessionend}
                            <span class="text-muted-foreground">(ended <TimeAgo value={event.data.sessionend as string} />)</span>
                        {/if}
                    </Table.Cell>
                </Table.Row>
            </Table.Body>
        </Table.Root>
    {/if}

    <h3 class="mb-2 text-lg font-semibold">Session Events</h3>

    {#if sessionEventsQuery.isLoading}
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
    {:else if sessionEventsQuery.data && sessionEventsQuery.data.length > 0}
        <Table.Root>
            <Table.Header>
                <Table.Row>
                    <Table.Head>Summary</Table.Head>
                    <Table.Head class="w-32">When</Table.Head>
                </Table.Row>
            </Table.Header>
            <Table.Body>
                {#each sessionEventsQuery.data as sessionEvent (sessionEvent.id)}
                    <Table.Row class="cursor-pointer hover:bg-muted/50">
                        <Table.Cell>
                            <A href={resolve('/(app)/event/[eventId]', { eventId: sessionEvent.id })}>
                                {sessionEvent.id}
                            </A>
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
