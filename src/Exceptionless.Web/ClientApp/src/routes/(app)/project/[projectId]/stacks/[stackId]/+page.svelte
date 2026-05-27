<script lang="ts">
    import type { IFilter } from '$comp/faceted-filter';
    import type { ProblemDetails } from '@exceptionless/fetchclient';

    import { goto } from '$app/navigation';
    import { resolve } from '$app/paths';
    import { page } from '$app/state';
    import { H3, Muted } from '$comp/typography';
    import { showBillingDialogOnUpgradeProblem } from '$features/billing';
    import { getStackEventsQuery } from '$features/events/api.svelte';
    import EventsOverview from '$features/events/components/events-overview.svelte';
    import { organization } from '$features/organizations/context.svelte';
    import StackCard from '$features/stacks/components/stack-card.svelte';
    import { watch } from 'runed';
    import { toast } from 'svelte-sonner';

    import { redirectToEventsWithFilter } from '../../../../redirect-to-events.svelte.js';

    const stackId = $derived(page.params.stackId || '');
    let eventId = $state<null | string>(null);

    const stackEventsQuery = getStackEventsQuery({
        params: {
            limit: 1,
            sort: '-date'
        },
        route: {
            get stackId() {
                return stackId;
            }
        }
    });

    $effect(() => {
        if (stackEventsQuery.isSuccess) {
            eventId = stackEventsQuery.data?.[0]?.id ?? null;
        }
    });

    watch(
        () => organization.current,
        () => {
            goto(resolve('/(app)/project/[projectId]/stacks', { projectId: page.params.projectId || '' }));
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

        toast.error('Unable to load stack event details.');
    }
</script>

<div class="flex flex-col gap-4">
    <H3>Stack Details</H3>
    {#if stackEventsQuery.isSuccess && !eventId}
        <StackCard {filterChanged} id={stackId} />
        <Muted>This stack has no events to display.</Muted>
    {:else if eventId}
        <EventsOverview {filterChanged} {handleError} id={eventId} />
    {/if}
</div>
