<script lang="ts">
    import type { IFilter } from '$comp/faceted-filter';
    import type { PersistentEvent } from '$features/events/models';
    import type { Stack } from '$features/stacks/models';
    import type { ProblemDetails } from '@foundatiofx/fetchclient';

    import { resolve } from '$app/paths';
    import DetailSheet from '$comp/detail-sheet.svelte';
    import { assistantPageContext } from '$features/assistant/page-context.svelte';
    import { buildEventDetailsHref } from '$features/events/components/summary';
    import { onDestroy } from 'svelte';

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
    const assistantContextOwner = Symbol('stack-detail-sheet');

    const resolvedHref = $derived(
        currentEventDetails
            ? buildEventDetailsHref(currentEventDetails.eventId, currentEventDetails.stackId)
            : stackId
              ? resolve('/(app)/stack/[stackId=objectid]', { stackId })
              : '#'
    );

    function handleEventLoaded(event: PersistentEvent): void {
        currentEventDetails = { eventId: event.id, stackId: event.stack_id };
        assistantPageContext.setOverlayEvent(assistantContextOwner, event);
    }

    function handleStackLoaded(stack: Stack): void {
        assistantPageContext.setOverlayStack(assistantContextOwner, stack);
    }

    function handleClose(): void {
        assistantPageContext.clearOverlay(assistantContextOwner);
        onClose();
    }

    $effect(() => {
        if (stackId !== lastStackId) {
            lastStackId = stackId ?? null;
            currentEventDetails = undefined;
            if (stackId) {
                assistantPageContext.setOverlay(assistantContextOwner, { stackId });
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

<DetailSheet detailsHref={resolvedHref} onClose={handleClose} open={!!stackId} title="Stack">
    {#if stackId}
        <StackDetails {filterChanged} {handleError} onDeleted={handleClose} onEventLoaded={handleEventLoaded} onStackLoaded={handleStackLoaded} {stackId} />
    {/if}
</DetailSheet>
