<script lang="ts">
    import type { StackStatus } from '$features/stacks/models';

    import { resolve } from '$app/paths';
    import { A, Muted } from '$comp/typography';
    import StackStatusBadge from '$features/stacks/components/stack-status-badge.svelte';
    import ChevronRight from '@lucide/svelte/icons/chevron-right';

    import type { StackSummaryModel, SummaryModel, SummaryTemplateKeys } from './index';

    interface Props {
        badgeStatus: StackStatus;
        showBadge: boolean;
        summary: SummaryModel<SummaryTemplateKeys>;
    }

    let { badgeStatus, showBadge, summary }: Props = $props();
    let source = $derived(summary as StackSummaryModel<'stack-error-summary'>);
</script>

<div class="line-clamp-2">
    {#if showBadge}
        <StackStatusBadge status={badgeStatus} />
    {/if}

    <strong>
        <abbr title={source.data.TypeFullName}>{source.data.Type}</abbr>
        {#if !source.data.Method}
            :
        {/if}
    </strong>

    {#if source.data.Method}
        in
        <strong>
            <abbr title={source.data.MethodFullName}>{source.data.Method}</abbr>
        </strong>
    {/if}

    <A class="inline" href={`${resolve('/(app)')}?filter=stack:${source.id}`}>
        {source.title}
    </A>
</div>

{#if source.data.Path}
    <Muted class="hidden sm:block">
        <ChevronRight class="inline size-4" />
        <span class="line-clamp-1 inline">{source.data.Path}</span>
    </Muted>
{/if}
