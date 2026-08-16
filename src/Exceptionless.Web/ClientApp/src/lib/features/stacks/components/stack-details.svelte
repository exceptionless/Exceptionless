<script lang="ts">
    import type { IFilter } from '$comp/faceted-filter';
    import type { AssistantFixResource } from '$features/assistant/controls.svelte';
    import type { PersistentEvent } from '$features/events/models';
    import type { Stack } from '$features/stacks/models';
    import type { ProblemDetails } from '@foundatiofx/fetchclient';

    import { Muted } from '$comp/typography';
    import { getStackEventsQuery } from '$features/events/api.svelte';
    import EventsOverview from '$features/events/components/events-overview.svelte';

    import StackCard from './stack-card.svelte';

    interface Props {
        assistantResource?: AssistantFixResource;
        eventId?: null | string;
        filterChanged: (filter: IFilter) => void;
        handleError: (problem: ProblemDetails) => void;
        onDeleted?: () => void;
        onEventLoaded?: (event: PersistentEvent) => void;
        onNavigate?: (eventId: string) => void;
        onStackLoaded?: (stack: Stack) => void;
        prepareAssistantContext?: () => void;
        stackId: string;
    }

    let {
        assistantResource,
        eventId: initialEventId,
        filterChanged,
        handleError,
        onDeleted,
        onEventLoaded,
        onNavigate,
        onStackLoaded,
        prepareAssistantContext,
        stackId
    }: Props = $props();
    let resolvedAssistantResource = $derived(assistantResource ?? (initialEventId ? 'event' : 'stack'));

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

    const latestEvent = $derived(stackEventsQuery.data?.[0]);

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
        if (initialEventId || !latestEvent?.id) {
            return;
        }

        selectedEventId = latestEvent.id;
        onEventLoaded?.(latestEvent);
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
    <EventsOverview
        assistantResource={resolvedAssistantResource}
        expectedStackId={stackId}
        {filterChanged}
        id={selectedEventId}
        {handleError}
        {onEventLoaded}
        onNavigate={handleNavigate}
        prepareStackAssistantContext={prepareAssistantContext}
    />
{:else if stackEventsQuery.isSuccess && !latestEvent?.id}
    <section>
        <h4 class="text-muted-foreground mb-3 text-sm font-semibold tracking-wide uppercase">Stack</h4>
        <StackCard
            assistantResource={resolvedAssistantResource}
            {filterChanged}
            id={stackId}
            {onDeleted}
            onError={handleError}
            onLoaded={onStackLoaded}
            {prepareAssistantContext}
        />
    </section>
    <Muted class="mt-4">No events available for this stack.</Muted>
{/if}
