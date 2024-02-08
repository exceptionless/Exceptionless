<script lang="ts">
    import { Badge } from '$comp/ui/badge';
    import IconChevronRight from '~icons/mdi/chevron-right';
    import type { StackSummaryModel, SummaryModel, SummaryTemplateKeys } from '$lib/models/api';
    import Muted from '$comp/typography/Muted.svelte';
    import A from '$comp/typography/A.svelte';

    export let badgeClass: string;
    export let showBadge: boolean;
    export let summary: SummaryModel<SummaryTemplateKeys>;
    const source = summary as StackSummaryModel<'stack-simple-summary'>;
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
        <IconChevronRight class="inline" />
        <span class="line-clamp-1 inline">{source.data.Path}</span>
    </Muted>
{/if}
