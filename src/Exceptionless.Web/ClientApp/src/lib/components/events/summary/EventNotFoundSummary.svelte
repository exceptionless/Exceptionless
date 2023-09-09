<script lang="ts">
	import type { EventSummaryModel, SummaryModel, SummaryTemplateKeys } from '$lib/models/api';
	import { Exceptionless } from '@exceptionless/browser';

	export let badgeClass: string;
	export let showBadge: boolean;
	export let showStatus: boolean;
	export let showType: boolean;
	export let summary: SummaryModel<SummaryTemplateKeys>;
	const source = summary as EventSummaryModel<'event-notfound-summary'>;

	Exceptionless.submitLog(
		'EventNotFoundSummary',
		`Rendering Summary badgeClass=${badgeClass} showBadge=${showBadge} showStatus=${showStatus} showType=${showType}`,
		'trace'
	);
</script>

<div class="line-clamp-2">
	{#if showType}
		<strong>404</strong>:&nbsp;
	{/if}
	<a href="/event/{source.id}" class="inline">{source.data.Source}</a>
</div>
