<script lang="ts">
	import { Exceptionless } from '@exceptionless/browser';
	import type { StackSummaryModel, SummaryModel, SummaryTemplateKeys } from '$lib/models/api';

	export let badgeClass: string;
	export let showBadge: boolean;
	export let showStatus: boolean;
	export let showType: boolean;
	export let summary: SummaryModel<SummaryTemplateKeys>;
	const source = summary as StackSummaryModel<'stack-session-summary'>;

	Exceptionless.submitLog(
		'StackSessionSummary',
		`Rendering Summary badgeClass=${badgeClass} showBadge=${showBadge} showStatus=${showStatus} showType=${showType}`,
		'trace'
	);
</script>

<div class="line-clamp-2">
	{#if showBadge}
		<span class="label label-default {badgeClass}">
			{source.status}
		</span>
	{/if}

	{#if showType}
		<strong>Session</strong>:&nbsp;
	{/if}

	<a href="app.stack/{source.id}" class="inline">
		{source.title}
	</a>
</div>
