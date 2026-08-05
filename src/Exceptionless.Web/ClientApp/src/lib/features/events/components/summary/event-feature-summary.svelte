<script lang="ts">
    import type { EventSummaryModel, SummaryModel, SummaryTemplateKeys } from './index';

    import EventSummaryLink from './event-summary-link.svelte';

    interface EventFeatureSummaryProps {
        linkToDetails?: boolean;
        showType: boolean;
        summary: SummaryModel<SummaryTemplateKeys>;
    }

    let { linkToDetails = true, showType, summary }: EventFeatureSummaryProps = $props();
    let source = $derived(summary as EventSummaryModel<'event-feature-summary'>);
</script>

<div class="line-clamp-2">
    {#if showType}
        <strong>Feature:&nbsp;</strong>
    {/if}
    <EventSummaryLink eventId={source.id} {linkToDetails}>{source.data.Source}</EventSummaryLink>
</div>
