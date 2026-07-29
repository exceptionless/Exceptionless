<script lang="ts">
    import type { EventSummaryModel, SummaryModel, SummaryTemplateKeys } from './index';

    import EventSummaryLink from './event-summary-link.svelte';

    interface EventFeatureSummaryProps {
        linkToDetails?: boolean;
        showType: boolean;
        summary: SummaryModel<SummaryTemplateKeys>;
    }

    let { linkToDetails = true, showType, summary }: EventFeatureSummaryProps = $props();
    let source = $derived(summary as EventSummaryModel<'event-summary'>);
</script>

<div class="line-clamp-2">
    {#if showType}
        <strong>{source.data.Type}</strong>
    {/if}
    {#if showType && source.data.Source}
        &nbsp;in&nbsp;
    {/if}
    {#if source.data.Source}
        <strong>{source.data.Source}</strong>
    {/if}
    {#if showType || source.data.Source}
        :&nbsp;
    {/if}
    <EventSummaryLink eventId={source.id} {linkToDetails}>{source.data.Message}</EventSummaryLink>
</div>
