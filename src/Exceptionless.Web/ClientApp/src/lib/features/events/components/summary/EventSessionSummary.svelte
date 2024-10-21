<script lang="ts">
    import { A } from '$comp/typography';

    import type { EventSummaryModel, SummaryModel, SummaryTemplateKeys } from '.';

    interface EventFeatureSummaryProps {
        showType: boolean;
        summary: SummaryModel<SummaryTemplateKeys>;
    }

    let { showType, summary }: EventFeatureSummaryProps = $props();
    let source = $derived(summary as EventSummaryModel<'event-session-summary'>);
</script>

<div class="line-clamp-2">
    {#if showType}
        <strong>
            {#if source.data.Type === 'sessionend'}
                Session End
            {:else if source.data.Type === 'heartbeat'}
                Session Heartbeat
            {:else}
                Session
            {/if}
        </strong>:&nbsp;
    {/if}

    <A class="inline">
        {#if source.data.Name || source.data.Identity || source.data.SessionId}
            {source.data.Name || source.data.Identity || source.data.SessionId}
            {#if source.data.Name && source.data.Identity}
                <span class="text-muted"> ({source.data.Identity})</span>
            {/if}
        {/if}
    </A>
</div>
