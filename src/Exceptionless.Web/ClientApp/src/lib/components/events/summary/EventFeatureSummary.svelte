<script lang="ts">
	import { Exceptionless } from '@exceptionless/browser';

	import type { EventSummaryModel, SummaryModel, SummaryTemplateKeys } from '$lib/models/api';
	export let badgeClass: string;
	export let showBadge: boolean;
	export let showStatus: boolean;
	export let showType: boolean;
	export let summary: SummaryModel<SummaryTemplateKeys>;
	const source = summary as EventSummaryModel<'event-feature-summary'>;

	Exceptionless.submitLog(
		'EventFeatureSummary',
		`Rendering Summary badgeClass=${badgeClass} showBadge=${showBadge} showStatus=${showStatus} showType=${showType}`,
		'trace'
	);
</script>

<div class="line-clamp-2">
	{#if showType}
		<strong>Feature:&nbsp;</strong>
	{/if}
	<a href="/event/{source.id}" class="inline">{source.data.Source}</a>
</div>
