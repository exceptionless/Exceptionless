<script lang="ts">
    import type { IFilter } from '$comp/faceted-filter';
    import type { ProblemDetails } from '@foundatiofx/fetchclient';

    import { goto } from '$app/navigation';
    import { resolve } from '$app/paths';
    import { page } from '$app/state';
    import { showBillingDialogOnUpgradeProblem } from '$features/billing';
    import { buildStackDetailsHref } from '$features/events/components/summary';
    import { organization } from '$features/organizations/context.svelte';
    import { getStackQuery } from '$features/stacks/api.svelte';
    import StackDetails from '$features/stacks/components/stack-details.svelte';
    import { toast } from 'svelte-sonner';

    import { getEventsNavigationOptionsForFilter, redirectToEventsWithFilter } from '../../../../redirect-to-events.svelte.js';

    const projectId = $derived(page.params.projectId || '');
    const stackId = $derived(page.params.stackId || '');
    const stackQuery = getStackQuery({
        route: {
            get id() {
                return stackId;
            }
        }
    });

    $effect(() => {
        if (stackQuery.isError) {
            handleError(stackQuery.error);
            return;
        }

        if (stackQuery.isSuccess && stackQuery.data.project_id !== projectId) {
            void goto(buildStackDetailsHref(stackQuery.data.id), {
                replaceState: true
            });
        }
    });

    async function filterChanged(addedOrUpdated: IFilter) {
        await redirectToEventsWithFilter(organization.current, addedOrUpdated, getEventsNavigationOptionsForFilter(addedOrUpdated));
    }

    async function handleDeleted() {
        await goto(
            resolve('/(app)/project/[projectId]/stacks', {
                projectId
            })
        );
    }

    function handleError(problem: ProblemDetails) {
        if (showBillingDialogOnUpgradeProblem(problem, organization.current)) {
            return;
        }

        toast.error('Unable to load stack event details.');
    }

    $effect(() => {
        document.title = 'Stack Details - Exceptionless';
    });
</script>

{#if stackQuery.isSuccess && stackQuery.data.project_id === projectId}
    <StackDetails {filterChanged} {handleError} onDeleted={handleDeleted} {stackId} />
{/if}
