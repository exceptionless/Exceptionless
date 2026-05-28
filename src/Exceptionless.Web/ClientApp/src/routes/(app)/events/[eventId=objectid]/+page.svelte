<script lang="ts">
    import type { ProblemDetails } from '@exceptionless/fetchclient';

    import { goto } from '$app/navigation';
    import { resolve } from '$app/paths';
    import { page } from '$app/state';
    import * as FacetedFilter from '$comp/faceted-filter';
    import { showBillingDialogOnUpgradeProblem } from '$features/billing';
    import EventsOverview from '$features/events/components/events-overview.svelte';
    import { organization } from '$features/organizations/context.svelte';
    import { watch } from 'runed';
    import { toast } from 'svelte-sonner';

    import { redirectToEventsWithFilter } from '../../redirect-to-events.svelte.js';

    // TODO: Have this happen automatically when the organization changes.
    watch(
        () => organization.current,
        () => {
            goto(resolve('/(app)/events'));
        },
        { lazy: true }
    );

    async function filterChanged(addedOrUpdated: FacetedFilter.IFilter) {
        await redirectToEventsWithFilter(organization.current, addedOrUpdated);
    }

    async function handleError(problem: ProblemDetails) {
        if (showBillingDialogOnUpgradeProblem(problem, organization.current)) {
            return;
        }

        toast.error(`The event "${page.params.eventId}" could not be found.`);
        await goto(resolve('/(app)/events'));
    }

    $effect(() => {
        document.title = 'Event Details - Exceptionless';
    });
</script>

<EventsOverview
    {filterChanged}
    id={page.params.eventId || ''}
    {handleError}
    onNavigate={(newId) => goto(resolve('/(app)/events/[eventId=objectid]', { eventId: newId }))}
/>
