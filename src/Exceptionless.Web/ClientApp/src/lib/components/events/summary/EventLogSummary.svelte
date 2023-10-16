<script lang="ts">
	import type { EventSummaryModel, SummaryModel, SummaryTemplateKeys } from '$lib/models/api';
	import LogLevel from '../LogLevel.svelte';

	export let showType: boolean;
	export let summary: SummaryModel<SummaryTemplateKeys>;
	const source = summary as EventSummaryModel<'event-log-summary'>;
	const level = source.data.Level?.toLowerCase();
</script>

<div class="line-clamp-2">
	{#if level}<LogLevel {level} />{/if}

	{#if showType}
		<strong>Log</strong>
		{#if source.data.Source}&nbsp;in&nbsp;{/if}
	{/if}

	{#if source.data.Source}
		<strong>
			{#if source.data.SourceShortName}
				<abbr title={source.data.Source}>{source.data.SourceShortName}</abbr>
			{:else}
				{source.data.Source}
			{/if}
		</strong>
	{/if}
	{#if showType || source.data.Source}:&nbsp;{/if}
	<a href="/event/{source.id}" class="inline">{source.data.Message}</a>
</div>
