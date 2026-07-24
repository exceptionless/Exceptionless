<script lang="ts">
    import { goto } from '$app/navigation';
    import { resolve } from '$app/paths';
    import { page } from '$app/state';
    import { A, H3, Muted } from '$comp/typography';
    import { Button } from '$comp/ui/button';
    import { Spinner } from '$comp/ui/spinner';
    import { getEventsByReferenceQuery } from '$features/events/api.svelte';
    import { ReferenceFilter } from '$features/events/components/filters';
    import Summary from '$features/events/components/summary/summary.svelte';

    const referenceId = $derived(page.params.referenceId || '');
    const eventsQuery = getEventsByReferenceQuery({
        route: {
            get referenceId() {
                return referenceId;
            }
        }
    });
    const eventListHref = $derived(`${resolve('/(app)/event')}?filter=${encodeURIComponent(new ReferenceFilter(referenceId).toFilter())}&limit=20`);
    let redirectedEventId = $state<string>();

    $effect(() => {
        const event = eventsQuery.data?.length === 1 ? eventsQuery.data[0] : undefined;
        if (event?.id && event.id !== redirectedEventId) {
            redirectedEventId = event.id;
            void goto(resolve('/(app)/event/[eventId=objectid]', { eventId: event.id }), { replaceState: true });
        }
    });

    $effect(() => {
        document.title = 'Event Reference - Exceptionless';
    });
</script>

<div class="space-y-6">
    <div class="space-y-2">
        <H3>Event Reference</H3>
        <Muted>{referenceId}</Muted>
    </div>

    {#if eventsQuery.isPending}
        <div class="flex items-center gap-2 text-muted-foreground">
            <Spinner class="size-4" />
            <span>Loading events...</span>
        </div>
    {:else if eventsQuery.error}
        <div class="rounded-md border border-destructive/40 bg-destructive/5 p-4 text-sm text-destructive">Unable to load events for this reference.</div>
    {:else if (eventsQuery.data?.length ?? 0) === 0}
        <div class="space-y-3">
            <Muted>No events were found for this reference.</Muted>
            <Button variant="secondary" href={eventListHref}>Search Events</Button>
        </div>
    {:else if (eventsQuery.data?.length ?? 0) > 1}
        <div class="flex items-center justify-between gap-4">
            <Muted>Found {eventsQuery.data?.length} events for this reference.</Muted>
            <Button variant="secondary" href={eventListHref}>View In Events</Button>
        </div>

        <div class="space-y-3">
            {#each eventsQuery.data ?? [] as event (event.id)}
                <div class="rounded-md border p-4">
                    <Summary summary={event} />
                    <A class="mt-2 inline-block" href={resolve('/(app)/event/[eventId=objectid]', { eventId: event.id })}>Open Event</A>
                </div>
            {/each}
        </div>
    {/if}
</div>
