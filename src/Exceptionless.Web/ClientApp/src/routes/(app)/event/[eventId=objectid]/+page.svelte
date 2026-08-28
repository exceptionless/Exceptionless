<script lang="ts">
    import type { PersistentEvent } from '$features/events/models';
    import type { ProblemDetails } from '@foundatiofx/fetchclient';

    import { goto } from '$app/navigation';
    import { resolve } from '$app/paths';
    import { page } from '$app/state';
    import * as FacetedFilter from '$comp/faceted-filter';
    import { assistantPageContext } from '$features/assistant/page-context.svelte';
    import { showBillingDialogOnUpgradeProblem } from '$features/billing';
    import EventsOverview from '$features/events/components/events-overview.svelte';
    import { buildEventDetailsHref } from '$features/events/components/summary';
    import { organization } from '$features/organizations/context.svelte';
    import { toast } from 'svelte-sonner';

    import { getEventsNavigationOptionsForFilter, redirectToEventsWithFilter } from '../../redirect-to-events.svelte.js';

    async function filterChanged(addedOrUpdated: FacetedFilter.IFilter) {
        await redirectToEventsWithFilter(organization.current, addedOrUpdated, getEventsNavigationOptionsForFilter(addedOrUpdated));
    }

    async function handleError(problem: ProblemDetails) {
        if (showBillingDialogOnUpgradeProblem(problem, organization.current)) {
            return;
        }

        toast.error(`The event "${page.params.eventId}" could not be found.`);
        await goto(resolve('/(app)/event'));
    }

    async function handleEventLoaded(event: PersistentEvent) {
        organization.current = event.organization_id;
        assistantPageContext.setPageEvent(event);
        await goto(buildEventDetailsHref(event.id, event.stack_id), {
            replaceState: true
        });
    }

    $effect(() => {
        document.title = 'Event Details - Exceptionless';
    });
</script>

<EventsOverview
    {filterChanged}
    id={page.params.eventId || ''}
    {handleError}
    loadProjectDetails={false}
    onEventLoaded={handleEventLoaded}
    onNavigate={(newId) => goto(buildEventDetailsHref(newId))}
/>
