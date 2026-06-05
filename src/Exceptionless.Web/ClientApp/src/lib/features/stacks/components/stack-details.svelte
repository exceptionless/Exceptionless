<script lang="ts">
    import type { IFilter } from '$comp/faceted-filter';
    import type { StackSummaryModel, SummaryTemplateKeys } from '$features/events/components/summary';
    import type { PersistentEvent } from '$features/events/models';
    import type { ProblemDetails } from '@exceptionless/fetchclient';

    import { Muted } from '$comp/typography';
    import { getStackEventsQuery } from '$features/events/api.svelte';
    import EventsOverview from '$features/events/components/events-overview.svelte';

    import type { Stack } from '../models';

    import StackCard from './stack-card.svelte';

    interface Props {
        eventId?: null | string;
        filterChanged: (filter: IFilter) => void;
        handleError: (problem: ProblemDetails) => void;
        initialStackSummary?: null | StackSummaryModel<SummaryTemplateKeys>;
        onDeleted?: () => void;
        onEventLoaded?: (event: PersistentEvent) => void;
        onNavigate?: (eventId: string) => void;
        stackId: string;
    }

    let { eventId: initialEventId, filterChanged, handleError, initialStackSummary, onDeleted, onEventLoaded, onNavigate, stackId }: Props = $props();

    let selectedEventId = $state<null | string>(null);
    let selectedEvent = $state<null | PersistentEvent>(null);
    let lastStackId = $state('');
    let handledEventsErrorForStackId = $state('');

    const stackEventsQuery = getStackEventsQuery({
        enabled: () => !initialEventId,
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

    const initialStack = $derived.by((): null | Stack => {
        if (!selectedEvent || !initialStackSummary || initialStackSummary.id !== selectedEvent.stack_id) {
            return null;
        }

        const summaryData = initialStackSummary.data as { Type?: unknown };
        const summaryType = typeof summaryData.Type === 'string' ? summaryData.Type : '';

        return {
            allow_notifications: true,
            created_utc: initialStackSummary.first_occurrence,
            duplicate_signature: '',
            first_occurrence: initialStackSummary.first_occurrence,
            id: initialStackSummary.id,
            is_deleted: false,
            last_occurrence: initialStackSummary.last_occurrence,
            occurrences_are_critical: false,
            organization_id: selectedEvent.organization_id,
            project_id: selectedEvent.project_id,
            references: [],
            signature_hash: '',
            signature_info: {
                Source: selectedEvent.source ?? '',
                Type: selectedEvent.type ?? ''
            },
            status: initialStackSummary.status,
            tags: selectedEvent.tags ?? [],
            title: initialStackSummary.title,
            total_occurrences: initialStackSummary.total,
            type: selectedEvent.type ?? summaryType,
            updated_utc: initialStackSummary.last_occurrence
        };
    });

    $effect(() => {
        if (initialEventId) {
            selectedEventId = initialEventId;
            selectedEvent = null;
        } else if (stackId !== lastStackId) {
            lastStackId = stackId;
            handledEventsErrorForStackId = '';
            selectedEventId = null;
            selectedEvent = null;
        }
    });

    $effect(() => {
        if (!initialEventId && stackEventsQuery.isSuccess) {
            const event = stackEventsQuery.data?.[0] ?? null;
            selectedEventId = event?.id ?? null;
            selectedEvent = event;
        }
    });

    $effect(() => {
        if (!selectedEventId && stackEventsQuery.isError && handledEventsErrorForStackId !== stackId) {
            handledEventsErrorForStackId = stackId;
            handleError(stackEventsQuery.error);
        }
    });

    function handleNavigate(newEventId: string) {
        if (onNavigate) {
            onNavigate(newEventId);
        } else {
            selectedEventId = newEventId;
            selectedEvent = null;
        }
    }
</script>

{#if selectedEventId}
    <EventsOverview
        {filterChanged}
        id={selectedEventId}
        initialEvent={selectedEvent}
        {initialStack}
        {handleError}
        {onEventLoaded}
        onNavigate={handleNavigate}
    />
{:else if stackEventsQuery.isSuccess}
    <section>
        <h4 class="text-muted-foreground mb-3 text-sm font-semibold tracking-wide uppercase">Stack</h4>
        <StackCard {filterChanged} id={stackId} {initialStack} {onDeleted} onError={handleError} />
    </section>
    <Muted class="mt-4">No events available for this stack.</Muted>
{/if}
