<script lang="ts">
    import type { SummaryModel, SummaryTemplateKeys, EventSummaryModel } from '$lib/models/api';

    interface Props {
        summary: SummaryModel<SummaryTemplateKeys>;
    }

    let { summary }: Props = $props();
    let source = $derived(summary as EventSummaryModel<SummaryTemplateKeys>);
    let identity = $derived('Identity' in source.data && source.data.Identity ? (source.data.Identity as string) : null);
    let name = $derived('Name' in source.data && source.data.Name ? (source.data.Name as string) : null);
</script>

{#if name && identity}
    <abbr title="{name} ({identity})" class="line-clamp-1">
        {name}
    </abbr>
{:else if name || identity}
    <span class="line-clamp-1">
        {name || identity}
    </span>
{/if}
