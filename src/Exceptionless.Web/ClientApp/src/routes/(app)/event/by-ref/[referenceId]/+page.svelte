<script module lang="ts">
    export function formatReferenceResultCount(total: number, previewCount: number): string {
        return total > previewCount ? `Showing ${previewCount} of ${total} events for this reference.` : `Found ${total} events for this reference.`;
    }
</script>

<script lang="ts">
    import { goto } from '$app/navigation';
    import { resolve } from '$app/paths';
    import { page } from '$app/state';
    import { A, H3, Muted } from '$comp/typography';
    import { Button } from '$comp/ui/button';
    import { Spinner } from '$comp/ui/spinner';
    import { getEventsByReferenceQuery } from '$features/events/api.svelte';
    import { ReferenceFilter } from '$features/events/components/filters';
    import { buildEventDetailsHref } from '$features/events/components/summary';
    import Summary from '$features/events/components/summary/summary.svelte';

    const referenceId = $derived(page.params.referenceId || '');
    const eventsQuery = getEventsByReferenceQuery({
        route: {
            get referenceId() {
                return referenceId;
            }
        }
    });
    const events = $derived(eventsQuery.data?.data ?? []);
    const total = $derived(typeof eventsQuery.data?.meta.total === 'number' ? eventsQuery.data.meta.total : events.length);
    const eventListHref = $derived(`${resolve('/(app)/event')}?filter=${encodeURIComponent(new ReferenceFilter(referenceId).toFilter())}&limit=20`);
    let redirectedEventId = $state<string>();

    $effect(() => {
        const event = total === 1 ? events[0] : undefined;
        if (event?.id && event.id !== redirectedEventId) {
            redirectedEventId = event.id;
            void goto(buildEventDetailsHref(event.id), {
                replaceState: true
            });
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
        <div class="text-muted-foreground flex items-center gap-2">
            <Spinner class="size-4" />
            <span>Loading events...</span>
        </div>
    {:else if eventsQuery.error}
        <div class="border-destructive/40 bg-destructive/5 text-destructive rounded-md border p-4 text-sm">Unable to load events for this reference.</div>
    {:else if total === 0}
        <div class="space-y-3">
            <Muted>No events were found for this reference.</Muted>
            <Button variant="secondary" href={eventListHref}>Search Events</Button>
        </div>
    {:else if total > 1}
        <div class="flex items-center justify-between gap-4">
            <Muted>{formatReferenceResultCount(total, events.length)}</Muted>
            <Button variant="secondary" href={eventListHref}>View In Events</Button>
        </div>

        <div class="space-y-3">
            {#each events as event (event.id)}
                <div class="rounded-md border p-4">
                    <Summary summary={event} />
                    <A class="mt-2 inline-block" href={buildEventDetailsHref(event.id)}>Open Event</A>
                </div>
            {/each}
        </div>
    {/if}
</div>
