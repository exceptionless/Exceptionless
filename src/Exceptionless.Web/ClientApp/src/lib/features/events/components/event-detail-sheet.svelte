<script lang="ts">
    import type { IFilter } from '$comp/faceted-filter';
    import type { ProblemDetails } from '@exceptionless/fetchclient';

    import { resolve } from '$app/paths';
    import DetailSheet from '$comp/detail-sheet.svelte';

    import EventsOverview from './events-overview.svelte';

    interface Props {
        detailsHref?: string;
        eventId: null | string;
        filterChanged: (filter: IFilter) => void;
        onClose: () => void;
        onError?: (problem: ProblemDetails) => void;
    }

    let { detailsHref, eventId = $bindable(), filterChanged, onClose, onError }: Props = $props();

    const resolvedHref = $derived(detailsHref ?? (eventId ? resolve('/(app)/event/[eventId=objectid]', { eventId }) : '#'));

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
        <EventsOverview {filterChanged} id={eventId} {handleError} onNavigate={(newId) => (eventId = newId)} />
    {/if}
</DetailSheet>
