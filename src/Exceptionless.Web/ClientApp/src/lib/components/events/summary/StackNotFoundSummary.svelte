<script lang="ts">
	import type { StackSummaryModel, SummaryModel, SummaryTemplateKeys } from '$lib/models/api';

	export let badgeClass: string;
	export let showBadge: boolean;
	export const showStatus: boolean = false;
	export let showType: boolean;
	export let summary: SummaryModel<SummaryTemplateKeys>;
	const source = summary as StackSummaryModel<'stack-notfound-summary'>;
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
