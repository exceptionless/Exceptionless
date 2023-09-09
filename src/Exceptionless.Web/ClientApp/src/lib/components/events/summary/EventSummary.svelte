<script lang="ts">
	import { Exceptionless } from '@exceptionless/browser';
	import type { EventSummaryModel, SummaryModel, SummaryTemplateKeys } from '$lib/models/api';

	export let badgeClass: string;
	export let showBadge: boolean;
	export let showStatus: boolean;
	export let showType: boolean;
	export let summary: SummaryModel<SummaryTemplateKeys>;
	const source = summary as EventSummaryModel<'event-summary'>;

	Exceptionless.submitLog(
		'EventSummary',
		`Rendering Summary badgeClass=${badgeClass} showBadge=${showBadge} showStatus=${showStatus} showType=${showType}`,
		'trace'
	);
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
<a href="app.event/{source.id}" class="inline line-clamp-2">{source.data.Message}</a>
