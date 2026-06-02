<script lang="ts">
    import type { IFilter } from '$comp/faceted-filter';
    import type { ProblemDetails } from '@exceptionless/fetchclient';

    import { goto } from '$app/navigation';
    import { resolve } from '$app/paths';
    import { page } from '$app/state';
    import { showBillingDialogOnUpgradeProblem } from '$features/billing';
    import { organization } from '$features/organizations/context.svelte';
    import StackDetails from '$features/stacks/components/stack-details.svelte';
    import { watch } from 'runed';
    import { toast } from 'svelte-sonner';

    import { redirectToEventsWithFilter } from '../../redirect-to-events.svelte.js';

    const stackId = $derived(page.params.stackId || '');

    watch(
        () => organization.current,
        () => {
            goto(resolve('/(app)/stack'));
        },
        { lazy: true }
    );

    async function filterChanged(addedOrUpdated: IFilter) {
        const options = addedOrUpdated.type === 'string' && addedOrUpdated.key === 'string-stack' ? { time: null } : undefined;
        await redirectToEventsWithFilter(organization.current, addedOrUpdated, options);
    }

    function handleError(problem: ProblemDetails) {
        if (showBillingDialogOnUpgradeProblem(problem, organization.current)) {
            return;
        }

        toast.error('Unable to load stack event details.');
    }

    async function handleDeleted() {
        await goto(resolve('/(app)/stack'));
    }

    $effect(() => {
        document.title = 'Stack Details - Exceptionless';
    });
</script>

<StackDetails {filterChanged} {handleError} onDeleted={handleDeleted} {stackId} />
