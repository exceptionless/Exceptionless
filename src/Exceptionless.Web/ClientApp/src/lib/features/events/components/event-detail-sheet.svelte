<script lang="ts">
    import type { IFilter } from '$comp/faceted-filter';
    import type { ProblemDetails } from '@exceptionless/fetchclient';

    import DetailSheet from '$comp/detail-sheet.svelte';

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

    const resolvedHref = $derived(
        detailsHref ?? (eventId ? buildEventDetailsHref(eventId, currentEventDetails?.eventId === eventId ? currentEventDetails.stackId : undefined) : '#')
    );

    function handleEventLoaded(event: PersistentEvent): void {
        currentEventDetails = { eventId: event.id, stackId: event.stack_id };
    }

    function handleError(problem: ProblemDetails) {
        if (onError) {
            onError(problem);
        } else {
            onClose();
        }
    }
</script>

<DetailSheet detailsHref={resolvedHref} {onClose} open={!!eventId} title="Event">
    {#if eventId}
        <EventsOverview {filterChanged} id={eventId} {handleError} onEventLoaded={handleEventLoaded} onNavigate={(newId) => (eventId = newId)} />
    {/if}
</DetailSheet>
