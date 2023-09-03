<script lang="ts">
	import type { StackSummaryModel, SummaryModel, SummaryTemplateKeys } from '$lib/models/api';

	export let badgeClass: string;
	export let showBadge: boolean;
	export let showStatus: boolean;
	export let showType: boolean;
	export let summary: SummaryModel<SummaryTemplateKeys>;
	const source = summary as StackSummaryModel<'stack-feature-summary'>;

	function truncateText(text?: string, maxLines?: number) {
		// Implement your text truncation logic here, or use a library like 'svelte-truncate'
		// to handle truncation.
		return text;
	}
</script>

{#if showBadge}
	<span class="label label-default {badgeClass}">
		{source.status}
	</span>
{/if}

{#if showType}
	<strong>Feature</strong>:&nbsp;
{/if}

<a href="/stack/{source.id}" class="truncate" style="max-lines: 2">
	{source.title}
</a>
