<script lang="ts">
    import { A, Muted } from '$comp/typography';
    import ChevronRight from '@lucide/svelte/icons/chevron-right';

    import type { EventSummaryModel, SummaryModel, SummaryTemplateKeys } from './index';

    interface Props {
        summary: SummaryModel<SummaryTemplateKeys>;
    }

    let { summary }: Props = $props();
    let source = $derived(summary as EventSummaryModel<'event-simple-summary'>);
</script>

<div class="line-clamp-2">
    <strong><abbr title={source.data.TypeFullName}>{source.data.Type}</abbr>: </strong>
    <A class="inline" href={`/next/event/${source.id}`}>{source.data.Message}</A>
</div>

{#if source.data.Path}
    <Muted class="hidden sm:block">
        <ChevronRight class="inline size-4" />
        <span class="line-clamp-1 inline">{source.data.Path}</span>
    </Muted>
{/if}
