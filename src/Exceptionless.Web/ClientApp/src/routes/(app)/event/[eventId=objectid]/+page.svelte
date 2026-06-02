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
            goto(resolve('/(app)/event'));
        },
        { lazy: true }
    );

    async function filterChanged(addedOrUpdated: FacetedFilter.IFilter) {
        const options = addedOrUpdated.type === 'string' && addedOrUpdated.key === 'string-stack' ? { time: null } : undefined;
        await redirectToEventsWithFilter(organization.current, addedOrUpdated, options);
    }

    async function handleError(problem: ProblemDetails) {
        if (showBillingDialogOnUpgradeProblem(problem, organization.current)) {
            return;
        }

        toast.error(`The event "${page.params.eventId}" could not be found.`);
        await goto(resolve('/(app)/event'));
    }

    $effect(() => {
        document.title = 'Event Details - Exceptionless';
    });
</script>

<EventsOverview
    {filterChanged}
    id={page.params.eventId || ''}
    {handleError}
    onNavigate={(newId) => goto(resolve('/(app)/event/[eventId=objectid]', { eventId: newId }))}
/>
