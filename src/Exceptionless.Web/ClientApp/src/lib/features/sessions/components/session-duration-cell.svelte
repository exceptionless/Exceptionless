<script lang="ts">
    import type { EventSessionSummaryData, EventSummaryModel, SummaryTemplateKeys } from '$features/events/components/summary/index';

    import Duration from '$comp/formatters/duration.svelte';
    import Live from '$comp/live.svelte';
    import { getSessionSummaryDuration } from '$features/events/utils';

    interface Props {
        summary: EventSummaryModel<SummaryTemplateKeys>;
    }

    let { summary }: Props = $props();
</script>

{#snippet content()}
    {@const data = summary.data as EventSessionSummaryData}
    {@const isActive = !data?.SessionEnd}
    {@const durationValue = getSessionSummaryDuration(data, summary.date)}
    <Live live={isActive} liveTitle="Online" notLiveTitle="Ended" />
    {#if isActive || durationValue !== undefined}
        <Duration value={durationValue} />
    {/if}
{/snippet}

<div class="flex items-center gap-1.5">
    {@render content()}
</div>
