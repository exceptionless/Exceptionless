<script lang="ts">
    import type { EventSummaryModel, SummaryModel, SummaryTemplateKeys } from '$lib/models/api';

    interface Props {
        summary: SummaryModel<SummaryTemplateKeys>;
    }

    let { summary }: Props = $props();
    let source = $derived(summary as EventSummaryModel<SummaryTemplateKeys>);
    let identity = $derived('Identity' in source.data && source.data.Identity ? (source.data.Identity as string) : null);
    let name = $derived('Name' in source.data && source.data.Name ? (source.data.Name as string) : null);
</script>

{#if name && identity}
    <abbr class="line-clamp-1" title="{name} ({identity})">
        {name}
    </abbr>
{:else if name || identity}
    <span class="line-clamp-1">
        {name || identity}
    </span>
{/if}
