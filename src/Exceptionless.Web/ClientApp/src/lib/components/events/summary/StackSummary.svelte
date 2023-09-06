<script lang="ts">
	import type { StackSummaryModel, SummaryModel, SummaryTemplateKeys } from '$lib/models/api';

	export let badgeClass: string;
	export let showBadge: boolean;
	export const showStatus: boolean = false;
	export let showType: boolean;
	export let summary: SummaryModel<SummaryTemplateKeys>;
	const source = summary as StackSummaryModel<'stack-summary'>;

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

<a href="/stack/{source.id}" class="truncate" style="max-lines: 2">
	{source.title}
</a>
