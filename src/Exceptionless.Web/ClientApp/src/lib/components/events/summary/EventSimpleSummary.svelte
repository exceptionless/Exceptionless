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
	<A href="/event/{source.id}" class="inline">{source.data.Message}</A>
</div>

{#if source.data.Path}
	<Muted class="hidden ml-6 sm:block">
		<IconChevronRight class="inline" />
		<span class="inline line-clamp-1">{source.data.Path}</span>
	</Muted>
{/if}
