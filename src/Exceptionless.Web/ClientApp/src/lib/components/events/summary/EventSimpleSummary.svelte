<script lang="ts">
	import IconChevronRight from '~icons/mdi/chevron-right';
	import type { EventSummaryModel, SummaryModel, SummaryTemplateKeys } from '$lib/models/api';

	export let summary: SummaryModel<SummaryTemplateKeys>;
	const source = summary as EventSummaryModel<'event-simple-summary'>;
</script>

<div class="line-clamp-2">
	<strong><abbr title={source.data.TypeFullName}>{source.data.Type}</abbr>: </strong>
	<a href="/event/{source.id}" class="inline">{source.data.Message}</a>
</div>

{#if source.data.Path}
	<div class="hidden sm:block text-gray-500 ml-6 text-sm">
		<IconChevronRight class="inline" />
		<span class="inline line-clamp-1">{source.data.Path}</span>
	</div>
{/if}
