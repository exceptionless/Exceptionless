<script lang="ts">
	import type { EventSummaryModel, SummaryModel, SummaryTemplateKeys } from '$lib/models/api';

	export let showType: boolean;
	export let summary: SummaryModel<SummaryTemplateKeys>;
	const source = summary as EventSummaryModel<'event-summary'>;
</script>

<div class="line-clamp-2">
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
	<a href="/event/{source.id}" class="inline">{source.data.Message}</a>
</div>
