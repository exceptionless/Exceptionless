<script lang="ts">
    import type { EventSessionSummaryData, EventSummaryModel, SummaryTemplateKeys } from '$features/events/components/summary/index';

    import Duration from '$comp/formatters/duration.svelte';
    import Live from '$comp/live.svelte';

    interface Props {
        summary: EventSummaryModel<SummaryTemplateKeys>;
    }

    let { summary }: Props = $props();
    const data = $derived(summary.data as EventSessionSummaryData);
    const isActive = $derived(!data?.SessionEnd);
    const durationMs = $derived.by(() => {
        if (data?.Value) {
            return parseFloat(data.Value) * 1000;
        }

        if (data?.SessionEnd && summary.date) {
            return new Date(data.SessionEnd).getTime() - new Date(summary.date).getTime();
        }

        return 0;
    });
    // For active sessions, use the event date so Duration live-updates
    const durationValue = $derived<Date | number>(isActive && summary.date ? new Date(summary.date) : durationMs);
</script>

<div class="flex items-center gap-1.5">
    <Live live={isActive} liveTitle="Online" notLiveTitle="Ended" />
    {#if isActive || durationMs > 0}
        <Duration value={durationValue} />
    {/if}
</div>
