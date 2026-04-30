<script lang="ts">
    import type { EventSessionSummaryData, EventSummaryModel, SummaryTemplateKeys } from '$features/events/components/summary/index';

    import Duration from '$comp/formatters/duration.svelte';
    import Live from '$comp/live.svelte';
    import { getSessionSummaryDuration } from '$features/events/utils';

    interface Props {
        summary: EventSummaryModel<SummaryTemplateKeys>;
    }

    let { summary }: Props = $props();
    const data = $derived(summary.data as EventSessionSummaryData);
    const isActive = $derived(!data?.SessionEnd);
    const durationValue = $derived(getSessionSummaryDuration(data, summary.date));
</script>

<div class="flex items-center gap-1.5">
    <Live live={isActive} liveTitle="Online" notLiveTitle="Ended" />
    {#if isActive || durationValue}
        <Duration value={durationValue} />
    {/if}
</div>
