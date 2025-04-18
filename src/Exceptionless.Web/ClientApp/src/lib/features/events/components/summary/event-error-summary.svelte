<script lang="ts">
    import { A, Muted } from '$comp/typography';
    import ChevronRight from '@lucide/svelte/icons/chevron-right';

    import type { EventSummaryModel, SummaryModel, SummaryTemplateKeys } from './index';

    interface Props {
        summary: SummaryModel<SummaryTemplateKeys>;
    }

    let { summary }: Props = $props();
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

    <A class="inline">
        {source.data.Message}
    </A>
</div>

{#if source.data.Path}
    <Muted class="hidden sm:block">
        <ChevronRight class="inline size-4" />
        <span class="line-clamp-1 inline">{source.data.Path}</span>
    </Muted>
{/if}
