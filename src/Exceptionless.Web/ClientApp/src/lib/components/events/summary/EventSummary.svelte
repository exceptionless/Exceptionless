<script lang="ts">
	import type { EventSummaryModel, SummaryModel, SummaryTemplateKeys } from '$lib/models/api';

	export let badgeClass: string;
	export let showBadge: boolean;
	export let showStatus: boolean;
	export let showType: boolean;
	export let summary: SummaryModel<SummaryTemplateKeys>;
	const source = summary as EventSummaryModel<'event-summary'>;

	function truncateText(text?: string, maxLines?: number) {
		// Implement your text truncation logic here, or use a library like 'svelte-truncate'
		// to handle truncation.
		return text;
	}
</script>

{#if showType}
	<strong>{source.data.Type}</strong>
{/if}
{#if showType && source.data.Source}
	&nbsp;in&nbsp;
{/if}
{#if source.data.Source}
	<strong>{source.data.Source}</strong>
{/if}
{#if showType || source.data.Source}
	:&nbsp;
{/if}
<a href="app.event/{source.id}" class="truncate" style="max-lines: 2"
	>{truncateText(source.data.Message)}</a
>
