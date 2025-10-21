<script lang="ts">
    import type { StackStatus } from '$features/stacks/models';

    import { A } from '$comp/typography';
    import StackStatusBadge from '$features/stacks/components/stack-status-badge.svelte';

    import type { StackSummaryModel, SummaryModel, SummaryTemplateKeys } from './index';

    interface Props {
        badgeStatus: StackStatus;
        showBadge: boolean;
        showType: boolean;
        summary: SummaryModel<SummaryTemplateKeys>;
    }

    let { badgeStatus, showBadge, showType, summary }: Props = $props();
    let source = $derived(summary as StackSummaryModel<'stack-summary'>);
</script>

<div class="line-clamp-2">
    {#if showBadge}
        <StackStatusBadge status={badgeStatus} />
    {/if}

    {#if showType}
        <strong>{source.data.Type}</strong>
    {/if}

    {#if showType && source.data.Source}
        &nbsp;in&nbsp;
    {/if}

    {#if source.data.Source}
        <strong>{source.data.Source}</strong>
    {/if}

    {#if showType || source.data.Source}
        :&nbsp;
    {/if}

    <A class="inline" href={`/next?filter=stack:${source.id}`}>
        {source.title}
    </A>
</div>
