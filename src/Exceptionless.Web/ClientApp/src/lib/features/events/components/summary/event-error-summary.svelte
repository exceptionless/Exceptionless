<script lang="ts">
    import { Muted } from '$comp/typography';
    import ChevronRight from '@lucide/svelte/icons/chevron-right';

    import type { EventSummaryModel, SummaryModel, SummaryTemplateKeys } from './index';

    import EventSummaryLink from './event-summary-link.svelte';

    interface Props {
        linkToDetails?: boolean;
        summary: SummaryModel<SummaryTemplateKeys>;
    }

    let { linkToDetails = true, summary }: Props = $props();
    let source = $derived(summary as EventSummaryModel<'event-error-summary'>);
</script>

<div class="line-clamp-2">
    {#if source.data.Type}
        <strong>
            <abbr title={source.data.TypeFullName}>{source.data.Type}</abbr>
            {#if !source.data.Method}:
            {/if}
        </strong>
    {/if}

    {#if source.data.Method}
        in
        <strong>
            <abbr title={source.data.MethodFullName}>{source.data.Method}</abbr>
        </strong>
    {/if}

    <EventSummaryLink eventId={source.id} {linkToDetails}>
        {source.data.Message}
    </EventSummaryLink>
</div>

{#if source.data.Path}
    <Muted class="hidden sm:block">
        <ChevronRight class="inline size-4" />
        <span class="line-clamp-1 inline">{source.data.Path}</span>
    </Muted>
{/if}
