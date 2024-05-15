<script lang="ts">
    import type { StackSummaryModel, SummaryModel, SummaryTemplateKeys } from '$lib/models/api';
    import { Badge } from '$comp/ui/badge';
    import { A } from '$comp/typography';

    export let badgeClass: string;
    export let showBadge: boolean;
    export let showType: boolean;
    export let summary: SummaryModel<SummaryTemplateKeys>;
    const source = summary as StackSummaryModel<'stack-log-summary'>;
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
