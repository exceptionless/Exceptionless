<script lang="ts">
	import { Exceptionless } from '@exceptionless/browser';
	import type { StackSummaryModel, SummaryModel, SummaryTemplateKeys } from '$lib/models/api';

	export let badgeClass: string;
	export let showBadge: boolean;
	export let showStatus: boolean;
	export let showType: boolean;
	export let summary: SummaryModel<SummaryTemplateKeys>;
	const source = summary as StackSummaryModel<'stack-notfound-summary'>;

	Exceptionless.submitLog(
		'StackNotFoundSummary',
		`Rendering Summary badgeClass=${badgeClass} showBadge=${showBadge} showStatus=${showStatus} showType=${showType}`,
		'trace'
	);
</script>

{#if showBadge}
	<span class="label label-default {badgeClass}">
		{source.status}
	</span>
{/if}

{#if showType}
	<strong>404</strong>:&nbsp;
{/if}
<a href="/stack/{source.id}" class="inline line-clamp-2">
	{source.title}
</a>
