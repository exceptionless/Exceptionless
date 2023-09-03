<script lang="ts">
	import IconChevronRight from '~icons/mdi/chevron-right';
	import type { EventSummaryModel, SummaryModel, SummaryTemplateKeys } from '$lib/models/api';

	export let badgeClass: string;
	export let showBadge: boolean;
	export let showStatus: boolean;
	export let showType: boolean;
	export let summary: SummaryModel<SummaryTemplateKeys>;
	const source = summary as EventSummaryModel<'event-error-summary'>;

	function truncateText(text?: string, maxLines?: number) {
		// Implement your text truncation logic here, or use a library like 'svelte-truncate'
		// to handle truncation.
		return text;
	}
</script>

<div>
	{#if source.data.Type}
		<strong>
			<abbr title={source.data.TypeFullName}>{source.data.Type}</abbr>
			{#if !source.data.Method}:
			{/if}
		</strong>
	{/if}

	{#if source.data.Method}
		in
		<strong>
			<abbr title={source.data.MethodFullName}>{source.data.Method}</abbr>
		</strong>
	{/if}

	<a href="/event/{source.id}" class="truncate" style="max-lines: 2">
		{truncateText(source.data.Message, 2)}
	</a>
</div>

{#if source.data.Path}
	<div class="hidden-xs error-path">
		<IconChevronRight />
		<span class="truncate">{source.data.Path}</span>
	</div>
{/if}
