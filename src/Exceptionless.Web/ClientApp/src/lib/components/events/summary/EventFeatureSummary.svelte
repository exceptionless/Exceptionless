<script lang="ts">
	import type { EventSummaryModel, SummaryModel, SummaryTemplateKeys } from '$lib/models/api';

	export let badgeClass: string;
	export let showBadge: boolean;
	export let showStatus: boolean;
	export let showType: boolean;
	export let summary: SummaryModel<SummaryTemplateKeys>;
	const source = summary as EventSummaryModel<'event-feature-summary'>;

	function truncateText(text?: string, maxLines?: number) {
		// Implement your text truncation logic here, or use a library like 'svelte-truncate'
		// to handle truncation.
		return text;
	}
</script>

<div>
	{#if showType}
		<strong>Feature:&nbsp;</strong>
	{/if}
	<a href="/event/{source.id}" class="truncate" style="max-lines: 2"
		>{truncateText(source.data.Source)}</a
	>
</div>
