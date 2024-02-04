<script lang="ts">
    import IconChevronRight from '~icons/mdi/chevron-right';
    import type { EventSummaryModel, SummaryModel, SummaryTemplateKeys } from '$lib/models/api';
    import Muted from '$comp/typography/Muted.svelte';
    import A from '$comp/typography/A.svelte';

    export let summary: SummaryModel<SummaryTemplateKeys>;
    const source = summary as EventSummaryModel<'event-simple-summary'>;
</script>

<div class="line-clamp-2">
    <strong><abbr title={source.data.TypeFullName}>{source.data.Type}</abbr>: </strong>
    <A class="inline">{source.data.Message}</A>
</div>

{#if source.data.Path}
    <Muted class="ml-6 hidden sm:block">
        <IconChevronRight class="inline" />
        <span class="line-clamp-1 inline">{source.data.Path}</span>
    </Muted>
{/if}
