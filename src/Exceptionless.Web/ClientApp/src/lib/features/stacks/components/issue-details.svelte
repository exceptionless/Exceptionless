<script lang="ts">
    import type { IFilter } from '$comp/faceted-filter';
    import type { ProblemDetails } from '@exceptionless/fetchclient';

    import { Muted } from '$comp/typography';
    import { getStackEventsQuery } from '$features/events/api.svelte';
    import EventsOverview from '$features/events/components/events-overview.svelte';

    import StackCard from './stack-card.svelte';

    interface Props {
        filterChanged: (filter: IFilter) => void;
        handleError: (problem: ProblemDetails) => void;
        stackId: string;
    }

    let { filterChanged, handleError, stackId }: Props = $props();

    let eventId = $state<null | string>(null);
    let lastStackId = $state('');

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
        if (stackId !== lastStackId) {
            lastStackId = stackId;
            eventId = null;
        }
    });

    $effect(() => {
        if (stackEventsQuery.isSuccess) {
            eventId = stackEventsQuery.data?.[0]?.id ?? null;
        }
    });
</script>

{#if stackEventsQuery.isSuccess && !eventId}
    <StackCard {filterChanged} id={stackId} />
    <Muted>This issue has no events to display.</Muted>
{:else if eventId}
    <EventsOverview {filterChanged} id={eventId} {handleError} />
{/if}
