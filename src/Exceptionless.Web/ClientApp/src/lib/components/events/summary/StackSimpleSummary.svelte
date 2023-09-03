<script lang="ts">
	import IconChevronRight from '~icons/mdi/chevron-right';
	import type { StackSummaryModel, SummaryModel, SummaryTemplateKeys } from '$lib/models/api';

	export let badgeClass: string;
	export let showBadge: boolean;
	export let showStatus: boolean;
	export let showType: boolean;
	export let summary: SummaryModel<SummaryTemplateKeys>;
	const source = summary as StackSummaryModel<'stack-simple-summary'>;

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

<div>
	<strong>
		<abbr title={source.data.TypeFullName}>{source.data.Type}</abbr>:
	</strong>

	<a href="app.stack/{source.id}" class="truncate" style="max-lines: 2">
		{source.title}
	</a>
</div>

{#if source.data.Path}
	<div class="hidden-xs error-path">
		<IconChevronRight />
		<span class="truncate">{source.data.Path}</span>
	</div>
{/if}
