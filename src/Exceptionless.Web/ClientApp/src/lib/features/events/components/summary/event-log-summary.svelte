<script lang="ts">
    import { A } from '$comp/typography';

    import type { EventSummaryModel, SummaryModel, SummaryTemplateKeys } from './index';

    import LogLevel from '../log-level.svelte';

    interface EventFeatureSummaryProps {
        showType: boolean;
        summary: SummaryModel<SummaryTemplateKeys>;
    }

    let { showType, summary }: EventFeatureSummaryProps = $props();
    let source = $derived(summary as EventSummaryModel<'event-log-summary'>);
    let level = $derived(source.data.Level?.toLowerCase());
</script>

<div class="line-clamp-2">
    {#if level}<LogLevel {level} />{/if}

    {#if showType}
        <strong>Log</strong>
        {#if source.data.Source}&nbsp;in&nbsp;{/if}
    {/if}

    {#if source.data.Source}
        <strong>
            {#if source.data.SourceShortName}
                <abbr title={source.data.Source}>{source.data.SourceShortName}</abbr>
            {:else}
                {source.data.Source}
            {/if}
        </strong>
    {/if}
    {#if showType || source.data.Source}:&nbsp;{/if}
    <A class="inline">{source.data.Message}</A>
</div>
