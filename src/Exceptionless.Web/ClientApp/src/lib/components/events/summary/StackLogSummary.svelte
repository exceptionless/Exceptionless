<script lang="ts">
    import type { StackSummaryModel, SummaryModel, SummaryTemplateKeys } from '$lib/models/api';

    import { A } from '$comp/typography';
    import { Badge } from '$comp/ui/badge';

    interface Props {
        badgeClass: string;
        showBadge: boolean;
        showType: boolean;
        summary: SummaryModel<SummaryTemplateKeys>;
    }

    let { badgeClass, showBadge, showType, summary }: Props = $props();
    let source = $derived(summary as StackSummaryModel<'stack-log-summary'>);
</script>

<div class="line-clamp-2">
    {#if showBadge}
        <Badge class={badgeClass}>
            {source.status}
        </Badge>
    {/if}

    {#if showType}
        <strong>Log source:</strong>&nbsp;
    {/if}

    <A class="inline">
        {#if source.data?.Source}
            <abbr title={source.data.Source}>{source.data.SourceShortName}</abbr>
        {:else}
            {source.title}
        {/if}
    </A>
</div>
