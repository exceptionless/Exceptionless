<script lang="ts">
    import type { IFilter } from '$comp/faceted-filter';
    import type { PersistentEvent } from '$features/events/models';
    import type { ProblemDetails } from '@exceptionless/fetchclient';

    import { goto } from '$app/navigation';
    import { resolve } from '$app/paths';
    import { page } from '$app/state';
    import { showBillingDialogOnUpgradeProblem } from '$features/billing';
    import { buildEventDetailsHref } from '$features/events/components/summary';
    import { organization } from '$features/organizations/context.svelte';
    import StackDetails from '$features/stacks/components/stack-details.svelte';
    import { watch } from 'runed';
    import { toast } from 'svelte-sonner';

    import { getEventsNavigationOptionsForFilter, redirectToEventsWithFilter } from '../../redirect-to-events.svelte.js';

    const stackId = $derived(page.params.stackId || '');

    watch(
        () => organization.current,
        () => {
            goto(resolve('/(app)/stack'));
        },
        { lazy: true }
    );

    async function filterChanged(addedOrUpdated: IFilter) {
        await redirectToEventsWithFilter(organization.current, addedOrUpdated, getEventsNavigationOptionsForFilter(addedOrUpdated));
    }

    async function handleError(problem: ProblemDetails) {
        if (showBillingDialogOnUpgradeProblem(problem, organization.current)) {
            return;
        }

        toast.error('Unable to load stack event details.');
        await goto(resolve('/(app)/stack'));
    }

    async function handleDeleted() {
        await goto(resolve('/(app)/stack'));
    }

    async function handleEventLoaded(event: PersistentEvent) {
        await goto(buildEventDetailsHref(event.id, event.stack_id), { replaceState: true });
    }

    async function handleNavigate(newEventId: string) {
        await goto(buildEventDetailsHref(newEventId, stackId));
    }

    $effect(() => {
        document.title = 'Stack Details - Exceptionless';
    });
</script>

<StackDetails {filterChanged} {handleError} onDeleted={handleDeleted} onEventLoaded={handleEventLoaded} onNavigate={handleNavigate} {stackId} />
