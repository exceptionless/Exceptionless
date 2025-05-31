<script lang="ts">
    import type { ProblemDetails } from '@exceptionless/fetchclient';

    import { goto } from '$app/navigation';
    import { page } from '$app/state';
    import * as FacetedFilter from '$comp/faceted-filter';
    import * as Card from '$comp/ui/card';
    import EventsOverview from '$features/events/components/events-overview.svelte';
    import { buildFilterCacheKey, toFilter, updateFilterCache } from '$features/events/components/filters/helpers.svelte';
    import { organization } from '$features/organizations/context.svelte';
    import { watch } from 'runed';
    import { toast } from 'svelte-sonner';

    // TODO: Have this happen automatically when the organization changes.
    watch(
        () => organization.current,
        () => {
            goto('/next/');
        },
        { lazy: true }
    );

    async function filterChanged(addedOrUpdated: FacetedFilter.IFilter) {
        // Prime the default page cache so that the filter is preserved and not a keyword filter.
        const filter = toFilter([addedOrUpdated]);
        const filterCacheKey = buildFilterCacheKey(organization.current, '/next/', filter);
        updateFilterCache(filterCacheKey, [addedOrUpdated]);

        await goto(`/next/?filter=${encodeURIComponent(filter)}`);
    }

    async function handleError(problem: ProblemDetails) {
        if (problem.status === 426) {
            // TODO: Show a message to the user that they need to upgrade their subscription.
        }

        toast.error(`The event "${page.params.eventId}" could not be found.`);
        await goto('/next/');
    }
</script>

<div class="flex flex-col space-y-4">
    <Card.Root
        ><Card.Header>
            <Card.Title class="text-2xl">Event Details</Card.Title></Card.Header
        >
        <Card.Content>
            <EventsOverview {filterChanged} id={page.params.eventId || ''} {handleError}></EventsOverview>
        </Card.Content>
    </Card.Root>
</div>
