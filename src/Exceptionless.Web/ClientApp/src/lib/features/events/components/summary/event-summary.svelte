<script lang="ts">
    import { A } from '$comp/typography';

    import type { EventSummaryModel, SummaryModel, SummaryTemplateKeys } from './index';

    interface EventFeatureSummaryProps {
        showType: boolean;
        summary: SummaryModel<SummaryTemplateKeys>;
    }

    let { showType, summary }: EventFeatureSummaryProps = $props();
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
    <A class="inline" href={`/next/event/${source.id}`}>{source.data.Message}</A>
</div>
