<script lang="ts">
    import type { ProblemDetails } from '@exceptionless/fetchclient';

    import { goto } from '$app/navigation';
    import { page } from '$app/state';
    import * as FacetedFilter from '$comp/faceted-filter';
    import { H3 } from '$comp/typography';
    import EventsOverview from '$features/events/components/events-overview.svelte';
    import { organization } from '$features/organizations/context.svelte';
    import { watch } from 'runed';
    import { toast } from 'svelte-sonner';

    import { redirectToEventsWithFilter } from '../../redirect-to-events.svelte.js';

    // TODO: Have this happen automatically when the organization changes.
    watch(
        () => organization.current,
        () => {
            goto('/next/');
        },
        { lazy: true }
    );

    async function filterChanged(addedOrUpdated: FacetedFilter.IFilter) {
        await redirectToEventsWithFilter(organization.current, addedOrUpdated);
    }

    async function handleError(problem: ProblemDetails) {
        if (problem.status === 426) {
            // TODO: Show a message to the user that they need to upgrade their subscription.
        }

        toast.error(`The event "${page.params.eventId}" could not be found.`);
        await goto('/next/');
    }
</script>

<div class="flex flex-col gap-4">
    <H3>Event Details</H3>
    <EventsOverview {filterChanged} id={page.params.eventId || ''} {handleError}></EventsOverview>
</div>
