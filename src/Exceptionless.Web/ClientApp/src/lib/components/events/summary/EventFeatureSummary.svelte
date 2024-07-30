<script lang="ts">
    import type { EventSummaryModel, SummaryModel, SummaryTemplateKeys } from '$lib/models/api';

    import { A } from '$comp/typography';

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
