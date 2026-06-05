<script lang="ts">
    import type { IFilter } from '$comp/faceted-filter';
    import type { PersistentEvent } from '$features/events/models';
    import type { ProblemDetails } from '@exceptionless/fetchclient';

    import { Muted } from '$comp/typography';
    import { getStackEventsQuery } from '$features/events/api.svelte';
    import EventsOverview from '$features/events/components/events-overview.svelte';

    import StackCard from './stack-card.svelte';

    interface Props {
        eventId?: null | string;
        filterChanged: (filter: IFilter) => void;
        handleError: (problem: ProblemDetails) => void;
        onDeleted?: () => void;
        onEventLoaded?: (event: PersistentEvent) => void;
        onNavigate?: (eventId: string) => void;
        stackId: string;
    }

    let { eventId: initialEventId, filterChanged, handleError, onDeleted, onEventLoaded, onNavigate, stackId }: Props = $props();

    let selectedEventId = $state<null | string>(null);
    let lastStackId = $state('');
    let handledEventsErrorForStackId = $state('');

    const stackEventsQuery = getStackEventsQuery({
        enabled: () => !initialEventId,
        params: {
            limit: 1,
            sort: '-date'
        },
        route: {
            get stackId() {
                return stackId;
            }
        }
    });

    $effect(() => {
        if (initialEventId) {
            selectedEventId = initialEventId;
        } else if (stackId !== lastStackId) {
            lastStackId = stackId;
            handledEventsErrorForStackId = '';
            selectedEventId = null;
        }
    });

    $effect(() => {
        if (!initialEventId && stackEventsQuery.isSuccess) {
            selectedEventId = stackEventsQuery.data?.[0]?.id ?? null;
        }
    });

    $effect(() => {
        if (!selectedEventId && stackEventsQuery.isError && handledEventsErrorForStackId !== stackId) {
            handledEventsErrorForStackId = stackId;
            handleError(stackEventsQuery.error);
        }
    });

    function handleNavigate(newEventId: string) {
        if (onNavigate) {
            onNavigate(newEventId);
        } else {
            selectedEventId = newEventId;
        }
    }
</script>

{#if selectedEventId}
    <EventsOverview {filterChanged} id={selectedEventId} {handleError} {onEventLoaded} onNavigate={handleNavigate} />
{:else if stackEventsQuery.isSuccess}
    <section>
        <h4 class="text-muted-foreground mb-3 text-sm font-semibold tracking-wide uppercase">Stack</h4>
        <StackCard {filterChanged} id={stackId} {onDeleted} onError={handleError} />
    </section>
    <Muted class="mt-4">No events available for this stack.</Muted>
{/if}
