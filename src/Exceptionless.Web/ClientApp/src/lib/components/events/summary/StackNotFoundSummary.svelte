<script lang="ts">
    import A from '$comp/typography/A.svelte';
    import { Badge } from '$comp/ui/badge';
    import type { StackSummaryModel, SummaryModel, SummaryTemplateKeys } from '$lib/models/api';

    export let badgeClass: string;
    export let showBadge: boolean;
    export let showType: boolean;
    export let summary: SummaryModel<SummaryTemplateKeys>;
    const source = summary as StackSummaryModel<'stack-notfound-summary'>;
</script>

<div class="line-clamp-2">
    {#if showBadge}
        <Badge class={badgeClass}>
            {source.status}
        </Badge>
    {/if}

    {#if showType}
        <strong>404</strong>:&nbsp;
    {/if}
    <A class="inline">
        {source.title}
    </A>
</div>
