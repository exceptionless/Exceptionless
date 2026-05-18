<script lang="ts">
    import type { IFilter } from '$comp/faceted-filter';
    import type { ProblemDetails } from '@exceptionless/fetchclient';

    import { resolve } from '$app/paths';
    import { Button } from '$comp/ui/button';
    import * as Sheet from '$comp/ui/sheet';
    import ExternalLink from '@lucide/svelte/icons/external-link';

    import EventsOverview from './events-overview.svelte';

    interface Props {
        detailsHref?: string;
        eventId: null | string;
        filterChanged: (filter: IFilter) => void;
        onClose: () => void;
        onError?: (problem: ProblemDetails) => void;
    }

    let { detailsHref, eventId = $bindable(), filterChanged, onClose, onError }: Props = $props();

    const resolvedHref = $derived(detailsHref ?? (eventId ? resolve('/(app)/event/[eventId]', { eventId }) : '#'));

    function handleOpenChange() {
        onClose();
    }

    function handleError(problem: ProblemDetails) {
        if (onError) {
            onError(problem);
        } else {
            onClose();
        }
    }
</script>

<Sheet.Root onOpenChange={handleOpenChange} open={!!eventId}>
    <Sheet.Content class="w-full overflow-y-scroll sm:max-w-full! md:w-5/6!">
        <Sheet.Header>
            <Sheet.Title>Event Details <Button href={resolvedHref} size="sm" title="Open in new window" variant="ghost"><ExternalLink /></Button></Sheet.Title>
        </Sheet.Header>
        <div class="px-4">
            {#if eventId}
                <EventsOverview {filterChanged} id={eventId} {handleError} />
            {/if}
        </div>
    </Sheet.Content>
</Sheet.Root>
