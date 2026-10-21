<script lang="ts">
    import { resolve } from '$app/paths';
    import ArrowRight from '@lucide/svelte/icons/arrow-right';

    import TimeAgo from '#comp/formatters/time-ago.svelte';
    import { Badge } from '#comp/ui/badge/index.ts';
    import { Button } from '#comp/ui/button/index.ts';
    import * as Dialog from '#comp/ui/dialog/index.ts';
    import { Skeleton } from '#comp/ui/skeleton/index.ts';
    import { getProjectLatestEventQuery } from '#features/events/api.svelte.ts';
    import { getErrorType, getMessage } from '#features/events/persistent-event.ts';

    import Confetti from './confetti.svelte';
    import ExieMascot from './exie-mascot.svelte';

    interface Props {
        isSampleData?: boolean;
        onSeeAllEvents: () => Promise<void> | void;
        open: boolean;
        projectId: string;
    }

    let { isSampleData = false, onSeeAllEvents, open = $bindable(false), projectId }: Props = $props();

    const POLL_TIMEOUT_MS = 30_000;
    let isPollingExpired = $state(false);

    // The notification can arrive before the event is searchable, so look again for a while, then fall back to the list.
    $effect(() => {
        if (!open) {
            return;
        }

        isPollingExpired = false;
        const timeout = setTimeout(() => (isPollingExpired = true), POLL_TIMEOUT_MS);
        return () => clearTimeout(timeout);
    });

    const latestEventQuery = getProjectLatestEventQuery({
        enabled: () => open,
        pollUntilFound: () => open && !isPollingExpired,
        route: {
            get projectId() {
                return projectId;
            }
        }
    });

    const event = $derived(latestEventQuery.data ?? undefined);
    const eventHref = $derived(
        event?.id
            ? resolve('/(app)/event/[eventId=objectid]', {
                  eventId: event.id
              })
            : undefined
    );
    const summary = $derived.by(() => {
        if (!event) {
            return undefined;
        }

        const simpleError = event.data?.['@simple_error'] as undefined | { message?: string };
        return getMessage(event) || simpleError?.message || event.source || 'Untitled event';
    });
    const errorType = $derived(event?.type === 'error' ? getErrorType(event) : undefined);
</script>

{#if open}
    <Confetti />
{/if}

<Dialog.Root bind:open>
    <Dialog.Content class="sm:max-w-md" data-first-event-celebration>
        <div class="flex flex-col items-center gap-3 pt-2 text-center">
            <ExieMascot mood="celebrate" size={96} />
            <Dialog.Header class="items-center text-center">
                <Dialog.Title class="text-xl">{isSampleData ? 'Your sample events are here!' : 'You caught your first event!'}</Dialog.Title>
                <Dialog.Description>
                    {#if isSampleData}
                        Have a look around. When your app is ready, connect it the same way and its events show up right alongside.
                    {:else}
                        Your app is talking to Exceptionless. From here on, I'll group repeats into stacks and let you know what's new.
                    {/if}
                </Dialog.Description>
            </Dialog.Header>
        </div>

        <div class="bg-muted/60 ring-foreground/10 flex min-h-16 flex-col gap-1 rounded-lg p-3 text-left ring-1" aria-live="polite">
            {#if event}
                <div class="flex flex-wrap items-center gap-2">
                    <Badge variant="outline">{event.type ?? 'event'}</Badge>
                    {#if errorType && errorType !== 'Unknown'}<span class="text-muted-foreground truncate font-mono text-xs">{errorType}</span>{/if}
                    <span class="text-muted-foreground ml-auto text-xs"><TimeAgo value={event.date} /></span>
                </div>
                <p class="text-foreground line-clamp-2 font-medium break-words">{summary}</p>
            {:else if latestEventQuery.isError || isPollingExpired}
                <p class="text-muted-foreground">Your event arrived, but I couldn't load its details. You'll find it with the rest of your events.</p>
            {:else}
                <Skeleton class="h-4 w-24" />
                <Skeleton class="h-5 w-full" />
                <span class="sr-only">Loading the event</span>
            {/if}
        </div>

        <Dialog.Footer class="flex-col-reverse gap-2 sm:flex-row sm:justify-end">
            {#if eventHref}
                <Button onclick={() => onSeeAllEvents()} variant="outline">See all events</Button>
                <Button href={eventHref}>
                    Take me to it <ArrowRight data-icon="inline-end" aria-hidden="true" />
                </Button>
            {:else if latestEventQuery.isError || isPollingExpired}
                <Button onclick={() => onSeeAllEvents()}>See all events</Button>
            {:else}
                <Button onclick={() => onSeeAllEvents()} variant="outline">See all events</Button>
                <Button disabled>Take me to it</Button>
            {/if}
        </Dialog.Footer>
    </Dialog.Content>
</Dialog.Root>
