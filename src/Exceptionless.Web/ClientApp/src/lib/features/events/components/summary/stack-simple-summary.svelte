<script lang="ts">
    import { A, Muted } from '$comp/typography';
    import { Badge } from '$comp/ui/badge';
    import ChevronRight from 'lucide-svelte/icons/chevron-right';

    import type { StackSummaryModel, SummaryModel, SummaryTemplateKeys } from './index';

    interface Props {
        badgeClass: string;
        showBadge: boolean;
        summary: SummaryModel<SummaryTemplateKeys>;
    }

    let { badgeClass, showBadge, summary }: Props = $props();
    let source = $derived(summary as StackSummaryModel<'stack-simple-summary'>);
</script>

{#if showBadge}
    <Badge class={badgeClass}>
        {source.status}
    </Badge>
{/if}

<div class="line-clamp-2">
    <strong>
        <abbr title={source.data.TypeFullName}>{source.data.Type}</abbr>:
    </strong>

    <A class="inline">{source.title}</A>
</div>

{#if source.data.Path}
    <Muted class="ml-6 hidden sm:block">
        <ChevronRight class="inline" />
        <span class="line-clamp-1 inline">{source.data.Path}</span>
    </Muted>
{/if}
