<script lang="ts">
    import type { ProblemDetails } from '@foundatiofx/fetchclient';

    import { goto } from '$app/navigation';
    import { resolve } from '$app/paths';
    import { page } from '$app/state';
    import { toast } from 'svelte-sonner';

    import type { PersistentEvent } from '#features/events/models/index.ts';

    import * as FacetedFilter from '#comp/faceted-filter/index.ts';
    import { assistantPageContext } from '#features/assistant/page-context.svelte.ts';
    import { showBillingDialogOnUpgradeProblem } from '#features/billing/index.ts';
    import EventsOverview from '#features/events/components/events-overview.svelte';
    import { buildEventDetailsHref } from '#features/events/components/summary/index.ts';
    import { organization } from '#features/organizations/context.svelte.ts';

    import { getEventsNavigationOptionsForFilter, redirectToEventsWithFilter } from '../../redirect-to-events.svelte.ts';

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
            replace: true
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
    onEventLoaded={handleEventLoaded}
    onNavigate={(newId) => goto(buildEventDetailsHref(newId))}
/>
