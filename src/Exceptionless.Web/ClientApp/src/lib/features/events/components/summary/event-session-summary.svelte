<script lang="ts">
    import type { EventSummaryModel, SummaryModel, SummaryTemplateKeys } from '.';

    import EventSummaryLink from './event-summary-link.svelte';

    interface EventFeatureSummaryProps {
        linkToDetails?: boolean;
        showType: boolean;
        summary: SummaryModel<SummaryTemplateKeys>;
    }

    let { linkToDetails = true, showType, summary }: EventFeatureSummaryProps = $props();
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

    <EventSummaryLink eventId={source.id} {linkToDetails}>
        {#if source.data.Name || source.data.Identity || source.data.SessionId}
            {source.data.Name || source.data.Identity || source.data.SessionId}
            {#if source.data.Name && source.data.Identity}
                <span class="text-muted-foreground"> ({source.data.Identity})</span>
            {/if}
        {/if}
    </EventSummaryLink>
</div>
