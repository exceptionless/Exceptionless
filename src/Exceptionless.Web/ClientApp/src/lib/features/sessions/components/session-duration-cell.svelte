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
    const durationMs = $derived(data?.Value ? parseFloat(data.Value) * 1000 : 0);
</script>

<div class="flex items-center gap-1.5">
    <Live live={isActive} liveTitle="Online" notLiveTitle="Ended" />
    {#if durationMs > 0}
        <Duration value={durationMs} />
    {:else if isActive}
        <span class="text-muted-foreground text-xs">Active</span>
    {/if}
</div>
