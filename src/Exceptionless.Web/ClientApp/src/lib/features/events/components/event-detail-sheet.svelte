<script lang="ts">
    import type { IFilter } from '$comp/faceted-filter';
    import type { ProblemDetails } from '@foundatiofx/fetchclient';

    import DetailSheet from '$comp/detail-sheet.svelte';
    import AssistantFixButton from '$features/assistant/components/assistant-fix-button.svelte';
    import { assistantPageContext } from '$features/assistant/page-context.svelte';
    import { onDestroy } from 'svelte';

    import type { PersistentEvent } from '../models';

    import EventsOverview from './events-overview.svelte';
    import { buildEventDetailsHref } from './summary';

    interface Props {
        detailsHref?: string;
        eventId: null | string;
        filterChanged: (filter: IFilter) => void;
        onClose: () => void;
        onError?: (problem: ProblemDetails) => void;
    }

    let { detailsHref, eventId = $bindable(), filterChanged, onClose, onError }: Props = $props();

    let currentEventDetails = $state<{ eventId: string; stackId: string }>();
    let currentEvent = $state<PersistentEvent>();
    let lastEventId = $state<null | string>(null);
    const assistantContextOwner = Symbol('event-detail-sheet');

    const resolvedHref = $derived(
        detailsHref ?? (eventId ? buildEventDetailsHref(eventId, currentEventDetails?.eventId === eventId ? currentEventDetails.stackId : undefined) : '#')
    );

    function handleEventLoaded(event: PersistentEvent): void {
        currentEvent = event;
        currentEventDetails = { eventId: event.id, stackId: event.stack_id };
        assistantPageContext.setOverlayEvent(assistantContextOwner, event);
    }

    function prepareAssistantContext(): void {
        if (currentEvent) {
            assistantPageContext.setOverlayEvent(assistantContextOwner, currentEvent);
        } else if (eventId) {
            assistantPageContext.setOverlay(assistantContextOwner, { eventId });
        }
    }

    function handleClose(): void {
        assistantPageContext.clearOverlay(assistantContextOwner);
        onClose();
    }

    $effect(() => {
        if (eventId !== lastEventId) {
            lastEventId = eventId;
            currentEvent = undefined;
            currentEventDetails = undefined;
            if (eventId) {
                assistantPageContext.setOverlay(assistantContextOwner, { eventId });
            } else {
                assistantPageContext.clearOverlay(assistantContextOwner);
            }
        }
    });

    onDestroy(() => assistantPageContext.clearOverlay(assistantContextOwner));

    function handleError(problem: ProblemDetails) {
        if (onError) {
            onError(problem);
        } else {
            handleClose();
        }
    }
</script>

<DetailSheet detailsHref={resolvedHref} onClose={handleClose} open={!!eventId} title="Event">
    {#snippet actions()}
        <AssistantFixButton prepareContext={prepareAssistantContext} resource="event" />
    {/snippet}
    {#if eventId}
        <div data-tour="event-details">
            <EventsOverview {filterChanged} id={eventId} {handleError} onEventLoaded={handleEventLoaded} onNavigate={(newId) => (eventId = newId)} />
        </div>
    {/if}
</DetailSheet>
