<script lang="ts">
    import type { PersistentEvent } from '$features/events/models';

    import { resolve } from '$app/paths';
    import Duration from '$comp/formatters/duration.svelte';
    import TimeAgo from '$comp/formatters/time-ago.svelte';
    import Live from '$comp/live.svelte';
    import { A } from '$comp/typography';
    import * as Alert from '$comp/ui/alert';
    import { Skeleton } from '$comp/ui/skeleton';
    import * as Table from '$comp/ui/table';
    import { getSessionStartDuration } from '$features/events/utils';
    import { getSessionId } from '$features/events/utils/index';
    import { usePremiumFeature } from '$features/organizations/hooks/use-premium-feature.svelte';
    import { getSessionEventsQuery } from '$features/sessions/api.svelte';
    import InfoIcon from '@lucide/svelte/icons/info';

    interface Props {
        event: PersistentEvent;
        hasPremiumFeatures?: boolean;
        time?: string;
    }

    let { event, hasPremiumFeatures = true, time }: Props = $props();

    const sessionId = $derived(getSessionId(event));
    const isSessionStart = $derived(event.type === 'session');

    const userInfo = $derived(event.data?.['@user']);
    const userIdentity = $derived(userInfo?.identity);
    const userName = $derived(userInfo?.name);

    const sessionEventsQuery = $derived(() =>
        getSessionEventsQuery({
            params: {
                filter: '-type:heartbeat',
                limit: 10,
                ...(time ? { time } : {})
            },
            route: {
                sessionId: hasPremiumFeatures ? sessionId : undefined
            }
        })
    );

    $effect(() => {
        if (!hasPremiumFeatures) usePremiumFeature('Sessions');
    });
</script>

{#if !hasPremiumFeatures}
    <Alert.Root variant="destructive" class="mb-4">
        <InfoIcon class="size-4" />
        <Alert.Title>Premium Feature</Alert.Title>
        <Alert.Description>Sessions are a premium feature. Upgrade your plan to view session events.</Alert.Description>
    </Alert.Root>
{/if}

<div class:opacity-60={!hasPremiumFeatures}>
    {#if isSessionStart}
        <Table.Root class="mb-4">
            <Table.Body>
                <Table.Row>
                    <Table.Head class="w-40 font-semibold whitespace-nowrap">Duration</Table.Head>
                    <Table.Cell class="w-4 pr-0"></Table.Cell>
                    <Table.Cell>
                        <Live live={!event.data?.sessionend} liveTitle="Online" notLiveTitle="Ended" />
                        <Duration value={getSessionStartDuration(event)} />
                        {#if event.data?.sessionend}
                            (ended <TimeAgo value={event.data.sessionend} />)
                        {/if}
                        <Live live={!event.data?.sessionend} liveTitle="Online" notLiveTitle="Ended" />
                        <Duration value={getSessionStartDuration(event)} />
                        {#if event.data?.sessionend}
                            (ended <TimeAgo value={event.data.sessionend} />)
                        {/if}
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

    <h3 class="mb-2 text-lg font-semibold">Session Events</h3>

    {#if sessionEventsQuery().isPending}
        <div class="space-y-2">
            {#each Array.from({ length: 5 }, (_, i) => i) as i (i)}
                <Skeleton class="h-10 w-full" />
            {/each}
        </div>
    {:else if sessionEventsQuery().isError}
        <Alert.Root variant="destructive">
            <Alert.Title>Error loading session events</Alert.Title>
            <Alert.Description>{sessionEventsQuery().error?.message ?? 'Unknown error'}</Alert.Description>
        </Alert.Root>
    {:else if (sessionEventsQuery().data ?? []).length > 0}
        <Table.Root>
            <Table.Header>
                <Table.Row>
                    <Table.Head>Summary</Table.Head>
                    <Table.Head class="w-32">Session Time</Table.Head>
                </Table.Row>
            </Table.Header>
            <Table.Body>
                {#each sessionEventsQuery().data ?? [] as sessionEvent (sessionEvent.id)}
                    <Table.Row class="hover:bg-muted/50 cursor-pointer">
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
