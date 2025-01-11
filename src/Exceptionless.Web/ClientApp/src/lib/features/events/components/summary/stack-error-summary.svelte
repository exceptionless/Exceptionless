<script lang="ts">
    import { A, Muted } from '$comp/typography';
    import { Badge } from '$comp/ui/badge';
    import IconChevronRight from '~icons/mdi/chevron-right';

    import type { StackSummaryModel, SummaryModel, SummaryTemplateKeys } from './index';

    interface Props {
        badgeClass: string;
        showBadge: boolean;
        summary: SummaryModel<SummaryTemplateKeys>;
    }

    let { badgeClass, showBadge, summary }: Props = $props();
    let source = $derived(summary as StackSummaryModel<'stack-error-summary'>);
</script>

<div class="line-clamp-2">
    {#if showBadge}
        <Badge class={badgeClass}>
            {source.status}
        </Badge>
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

    <A class="inline">
        {source.title}
    </A>
</div>

{#if source.data.Path}
    <Muted class="ml-6 hidden sm:block">
        <IconChevronRight class="inline" />
        <span class="line-clamp-1 inline">{source.data.Path}</span>
    </Muted>
{/if}
