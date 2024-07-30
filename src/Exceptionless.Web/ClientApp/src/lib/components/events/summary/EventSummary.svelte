<script lang="ts">
    import type { EventSummaryModel, SummaryModel, SummaryTemplateKeys } from '$lib/models/api';

    import { A } from '$comp/typography';

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
    <A class="inline">{source.data.Message}</A>
</div>
