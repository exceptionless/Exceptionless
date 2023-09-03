<script lang="ts">
	import IconChevronRight from '~icons/mdi/chevron-right';
	import type { EventSummaryModel, SummaryModel, SummaryTemplateKeys } from '$lib/models/api';

	export let badgeClass: string;
	export let showBadge: boolean;
	export let showStatus: boolean;
	export let showType: boolean;
	export let summary: SummaryModel<SummaryTemplateKeys>;
	const source = summary as EventSummaryModel<'event-simple-summary'>;

	function truncateText(text?: string, maxLines?: number) {
		// Implement your text truncation logic here, or use a library like 'svelte-truncate'
		// to handle truncation.
		return text;
	}
</script>

<div>
	<strong><abbr title={source.data.TypeFullName}>{source.data.Type}</abbr>: </strong>
	<a href="/event/{source.id}" class="truncate" style="max-lines: 2"
		>{truncateText(source.data.Message)}</a
	>
</div>

{#if source.data.Path}
	<div class="hidden-xs error-path">
		<IconChevronRight />
		<span class="truncate" style="max-lines: 1">{truncateText(source.data.Path)}</span>
	</div>
{/if}
