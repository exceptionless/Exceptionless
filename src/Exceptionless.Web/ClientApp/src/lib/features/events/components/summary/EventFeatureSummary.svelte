<script lang="ts">
    import { A } from '$comp/typography';

    import type { EventSummaryModel, SummaryModel, SummaryTemplateKeys } from './index';

    interface EventFeatureSummaryProps {
        showType: boolean;
        summary: SummaryModel<SummaryTemplateKeys>;
    }

    let { showType, summary }: EventFeatureSummaryProps = $props();
    let source = $derived(summary as EventSummaryModel<'event-feature-summary'>);
</script>

<div class="line-clamp-2">
    {#if showType}
        <strong>Feature:&nbsp;</strong>
    {/if}
    <A class="inline">{source.data.Source}</A>
</div>
