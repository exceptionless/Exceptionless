<script lang="ts">
	import IconChevronRight from '~icons/mdi/chevron-right';
	import type { StackSummaryModel, SummaryModel, SummaryTemplateKeys } from '$lib/models/api';

	export let badgeClass: string;
	export let showBadge: boolean;
	export let showStatus: boolean;
	export let showType: boolean;
	export let summary: SummaryModel<SummaryTemplateKeys>;
	const source = summary as StackSummaryModel<'stack-error-summary'>;

	function truncateText(text?: string, maxLines?: number) {
		// Implement your text truncation logic here, or use a library like 'svelte-truncate'
		// to handle truncation.
		return text;
	}
</script>

<div>
	{#if showBadge}
		<span class="label label-default {badgeClass}">
			{source.status}
		</span>
	{/if}

	<strong>
		<abbr title={source.data.TypeFullName}>{source.data.Type}</abbr>
		{#if !source.data.Method}
			:
		{/if}
	</strong>

	{#if source.data.Method}
		in
		<strong>
			<abbr title={source.data.MethodFullName}>{source.data.Method}</abbr>
		</strong>
	{/if}

	<a href="/stack/{source.id}" class="truncate" style="max-lines: 2">
		{source.title}
	</a>
</div>

{#if source.data.Path}
	<div class="hidden-xs error-path">
		<IconChevronRight />
		<span class="truncate">{source.data.Path}</span>
	</div>
{/if}
