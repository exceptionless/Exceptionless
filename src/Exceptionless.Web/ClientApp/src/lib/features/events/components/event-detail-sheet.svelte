<script lang="ts">
    import type { IFilter } from '$comp/faceted-filter';
    import type { ProblemDetails } from '@foundatiofx/fetchclient';

    import DetailSheet from '$comp/detail-sheet.svelte';
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
    let lastEventId = $state<null | string>(null);
    const assistantContextOwner = Symbol('event-detail-sheet');

    const resolvedHref = $derived(
        detailsHref ?? (eventId ? buildEventDetailsHref(eventId, currentEventDetails?.eventId === eventId ? currentEventDetails.stackId : undefined) : '#')
    );

    function handleEventLoaded(event: PersistentEvent): void {
        currentEventDetails = { eventId: event.id, stackId: event.stack_id };
        assistantPageContext.setOverlayEvent(assistantContextOwner, event);
    }

    function handleClose(): void {
        assistantPageContext.clearOverlay(assistantContextOwner);
        onClose();
    }

    $effect(() => {
        if (eventId !== lastEventId) {
            lastEventId = eventId;
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
    {#if eventId}
        <EventsOverview {filterChanged} id={eventId} {handleError} onEventLoaded={handleEventLoaded} onNavigate={(newId) => (eventId = newId)} />
    {/if}
</DetailSheet>
