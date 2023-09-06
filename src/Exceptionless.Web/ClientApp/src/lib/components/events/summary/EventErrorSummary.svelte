<script lang="ts">
	import IconChevronRight from '~icons/mdi/chevron-right';
	import type { EventSummaryModel, SummaryModel, SummaryTemplateKeys } from '$lib/models/api';

	export const badgeClass: string = '';
	export const showBadge: boolean = false;
	export const showStatus: boolean = false;
	export const showType: boolean = false;
	export let summary: SummaryModel<SummaryTemplateKeys>;
	const source = summary as EventSummaryModel<'event-error-summary'>;
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

	<a href="/event/{source.id}" class="inline line-clamp-2">
		{source.data.Message}
	</a>
</div>

{#if source.data.Path}
	<div class="hidden sm:block text-gray-500 ml-6 text-sm">
		<IconChevronRight class="inline" />
		<span class="inline line-clamp-1">{source.data.Path}</span>
	</div>
{/if}
