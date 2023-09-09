<script lang="ts">
	import { Exceptionless } from '@exceptionless/browser';
	import type { EventSummaryModel, SummaryModel, SummaryTemplateKeys } from '$lib/models/api';

	export let badgeClass: string;
	export let showBadge: boolean;
	export let showStatus: boolean;
	export let showType: boolean;
	export let summary: SummaryModel<SummaryTemplateKeys>;
	const source = summary as EventSummaryModel<'event-log-summary'>;

	const level = source.data.Level?.toLowerCase();
	const isLevelSuccess = level === 'trace' || level === 'debug';
	const isLevelInfo = level === 'info';
	const isLevelWarning = level === 'warn';
	const isLevelError = level === 'error';

	Exceptionless.submitLog(
		'EventLogSummary',
		`Rendering Summary badgeClass=${badgeClass} showBadge=${showBadge} showStatus=${showStatus} showType=${showType}`,
		'trace'
	);
</script>

{#if level}
	<span
		class="badge {isLevelSuccess && 'badge-success'} {isLevelInfo &&
			'badge-info'} {isLevelWarning && 'badge-warning'} {isLevelError && 'badge-error'}"
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
	<a href="app.event/{source.id}" class="inline line-clamp-2">{source.data.Message}</a>
{/if}
