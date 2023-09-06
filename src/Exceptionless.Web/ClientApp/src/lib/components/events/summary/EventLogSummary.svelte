<script lang="ts">
	import type { EventSummaryModel, SummaryModel, SummaryTemplateKeys } from '$lib/models/api';

	export const badgeClass: string = '';
	export const showBadge: boolean = false;
	export const showStatus: boolean = false;
	export let showType: boolean;
	export let summary: SummaryModel<SummaryTemplateKeys>;
	const source = summary as EventSummaryModel<'event-log-summary'>;

	const level = source.data.Level?.toLowerCase();
	const isLevelSuccess = level === 'trace' || level === 'debug';
	const isLevelInfo = level === 'info';
	const isLevelWarning = level === 'warn';
	const isLevelError = level === 'error';

	function truncateText(text?: string, maxLines?: number) {
		// Implement your text truncation logic here, or use a library like 'svelte-truncate'
		// to handle truncation.
		return text;
	}
</script>

{#if level}
	<span
		class="label label-default"
		class:label-success={isLevelSuccess}
		class:label-info={isLevelInfo}
		class:label-warning={isLevelWarning}
		class:label-danger={isLevelError}
	>
		{level}
	</span>
{/if}

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
	{#if showType || source.data.Source}:&nbsp;{/if}
	<a href="app.event/{source.id}" class="truncate" style="max-lines: 2"
		>{truncateText(source.data.Message)}</a
	>
{/if}
