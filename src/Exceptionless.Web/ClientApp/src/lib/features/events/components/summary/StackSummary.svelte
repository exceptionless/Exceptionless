<script lang="ts">
    import { A } from '$comp/typography';
    import { Badge } from '$comp/ui/badge';

    import type { StackSummaryModel, SummaryModel, SummaryTemplateKeys } from './index';

    interface Props {
        badgeClass: string;
        showBadge: boolean;
        showType: boolean;
        summary: SummaryModel<SummaryTemplateKeys>;
    }

    let { badgeClass, showBadge, showType, summary }: Props = $props();
    let source = $derived(summary as StackSummaryModel<'stack-summary'>);
</script>

<div class="line-clamp-2">
    {#if showBadge}
        <Badge class={badgeClass}>
            {source.status}
        </Badge>
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

    <A class="inline">
        {source.title}
    </A>
</div>
