<script lang="ts">
    import type { IFilter } from '$comp/faceted-filter';
    import type { PersistentEvent } from '$features/events/models';
    import type { ProblemDetails } from '@exceptionless/fetchclient';

    import { resolve } from '$app/paths';
    import DetailSheet from '$comp/detail-sheet.svelte';
    import { buildEventDetailsHref } from '$features/events/components/summary';

    import StackDetails from './stack-details.svelte';

    interface Props {
        filterChanged: (filter: IFilter) => void;
        onClose: () => void;
        onError?: (problem: ProblemDetails) => void;
        stackId: null | string | undefined;
    }

    let { filterChanged, onClose, onError, stackId = $bindable() }: Props = $props();

    let currentEventDetails = $state<{ eventId: string; stackId: string }>();
    let lastStackId = $state<null | string>(null);

    const resolvedHref = $derived(
        currentEventDetails
            ? buildEventDetailsHref(currentEventDetails.eventId, currentEventDetails.stackId)
            : stackId
              ? resolve('/(app)/stack/[stackId=objectid]', { stackId })
              : '#'
    );

    function handleEventLoaded(event: PersistentEvent): void {
        currentEventDetails = { eventId: event.id, stackId: event.stack_id };
    }

    $effect(() => {
        if (stackId !== lastStackId) {
            lastStackId = stackId ?? null;
            currentEventDetails = undefined;
        }
    });

    function handleError(problem: ProblemDetails) {
        if (onError) {
            onError(problem);
        } else {
            onClose();
        }
    }
</script>

<DetailSheet detailsHref={resolvedHref} {onClose} open={!!stackId} title="Stack">
    {#if stackId}
        <StackDetails {filterChanged} {handleError} onDeleted={onClose} onEventLoaded={handleEventLoaded} {stackId} />
    {/if}
</DetailSheet>
