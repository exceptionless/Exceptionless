<script lang="ts">
    import type { StackStatus } from '$features/stacks/models';

    import { A } from '$comp/typography';
    import StackStatusBadge from '$features/stacks/components/stack-status-badge.svelte';

    import { buildStackEventsHref, type StackSummaryModel, type SummaryModel, type SummaryTemplateKeys } from './index';

    interface Props {
        badgeStatus: StackStatus;
        showBadge: boolean;
        showType: boolean;
        summary: SummaryModel<SummaryTemplateKeys>;
    }

    let { badgeStatus, showBadge, showType, summary }: Props = $props();
    let source = $derived(summary as StackSummaryModel<'stack-notfound-summary'>);
</script>

<div class="line-clamp-2">
    {#if showBadge}
        <StackStatusBadge status={badgeStatus} />
    {/if}

    {#if showType}
        <strong>404</strong>:&nbsp;
    {/if}
    <A class="inline" href={buildStackEventsHref(source.id)}>
        {source.title}
    </A>
</div>
