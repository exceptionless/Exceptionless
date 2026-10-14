<script lang="ts">
    import type { ProblemDetails } from '@foundatiofx/fetchclient';

    import { goto } from '$app/navigation';
    import { resolve } from '$app/paths';
    import { page } from '$app/state';
    import { toast } from 'svelte-sonner';

    import type { IFilter } from '#comp/faceted-filter/index.ts';
    import type { PersistentEvent } from '#features/events/models/index.ts';

    import { assistantPageContext } from '#features/assistant/page-context.svelte.ts';
    import { showBillingDialogOnUpgradeProblem } from '#features/billing/index.ts';
    import { buildEventDetailsHref } from '#features/events/components/summary/index.ts';
    import { organization } from '#features/organizations/context.svelte.ts';
    import StackDetails from '#features/stacks/components/stack-details.svelte';

    import { getEventsNavigationOptionsForFilter, redirectToEventsWithFilter } from '../../../../redirect-to-events.svelte.ts';

    const stackId = $derived(page.params.stackId || '');
    const eventId = $derived(page.params.eventId || '');

    async function filterChanged(addedOrUpdated: IFilter) {
        await redirectToEventsWithFilter(organization.current, addedOrUpdated, getEventsNavigationOptionsForFilter(addedOrUpdated));
    }

    async function handleDeleted() {
        await goto(resolve('/(app)/stack'));
    }

    function handleError(problem: ProblemDetails) {
        if (showBillingDialogOnUpgradeProblem(problem, organization.current)) {
            return;
        }

        toast.error(problem.title ?? problem.detail ?? 'Unable to load stack event details.');
    }

    async function handleEventLoaded(event: PersistentEvent) {
        organization.current = event.organization_id;
        assistantPageContext.setPageEvent(event);

        if (event.id !== eventId || event.stack_id !== stackId) {
            await goto(buildEventDetailsHref(event.id, event.stack_id), {
                replace: true
            });
        }
    }

    async function handleNavigate(newEventId: string) {
        await goto(buildEventDetailsHref(newEventId, stackId));
    }

    $effect(() => {
        document.title = 'Stack Event Details - Exceptionless';
    });
</script>

<StackDetails {eventId} {filterChanged} {handleError} onDeleted={handleDeleted} onEventLoaded={handleEventLoaded} onNavigate={handleNavigate} {stackId} />
