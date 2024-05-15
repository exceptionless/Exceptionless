<script lang="ts">
    import { A } from '$comp/typography';
    import type { EventSummaryModel, SummaryModel, SummaryTemplateKeys } from '$lib/models/api';

    export let showType: boolean;
    export let summary: SummaryModel<SummaryTemplateKeys>;
    const source = summary as EventSummaryModel<'event-feature-summary'>;
</script>

<div class="line-clamp-2">
    {#if showType}
        <strong>Feature:&nbsp;</strong>
    {/if}
    <A class="inline">{source.data.Source}</A>
</div>
