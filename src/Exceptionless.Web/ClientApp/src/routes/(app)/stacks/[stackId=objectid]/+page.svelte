<script lang="ts">
    import type { IFilter } from '$comp/faceted-filter';
    import type { ProblemDetails } from '@exceptionless/fetchclient';

    import { goto } from '$app/navigation';
    import { resolve } from '$app/paths';
    import { page } from '$app/state';
    import { H3 } from '$comp/typography';
    import { showBillingDialogOnUpgradeProblem } from '$features/billing';
    import { organization } from '$features/organizations/context.svelte';
    import IssueDetails from '$features/stacks/components/stack-details.svelte';
    import { watch } from 'runed';
    import { toast } from 'svelte-sonner';

    import { redirectToEventsWithFilter } from '../../redirect-to-events.svelte.js';

    const stackId = $derived(page.params.stackId || '');

    watch(
        () => organization.current,
        () => {
            goto(resolve('/(app)/stacks'));
        },
        { lazy: true }
    );

    async function filterChanged(addedOrUpdated: IFilter) {
        await redirectToEventsWithFilter(organization.current, addedOrUpdated);
    }

    function handleError(problem: ProblemDetails) {
        if (showBillingDialogOnUpgradeProblem(problem, organization.current)) {
            return;
        }

        toast.error('Unable to load issue event details.');
    }
</script>

<div class="flex flex-col gap-4">
    <H3>Issue Details</H3>
    <IssueDetails {filterChanged} {handleError} {stackId} />
</div>
