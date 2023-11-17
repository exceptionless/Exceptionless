<script lang="ts">
	import { Badge } from '$comp/ui/badge';
	import IconChevronRight from '~icons/mdi/chevron-right';
	import type { StackSummaryModel, SummaryModel, SummaryTemplateKeys } from '$lib/models/api';

	export let badgeClass: string;
	export let showBadge: boolean;
	export let summary: SummaryModel<SummaryTemplateKeys>;
	const source = summary as StackSummaryModel<'stack-error-summary'>;
</script>

<div class="line-clamp-2">
	{#if showBadge}
		<Badge class={badgeClass}>
			{source.status}
		</Badge>
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

	<a href="/stack/{source.id}" class="inline">
		{source.title}
	</a>
</div>

{#if source.data.Path}
	<div class="hidden sm:block ml-6 text-sm text-gray-500 dark:text-gray-400">
		<IconChevronRight class="inline" />
		<span class="inline line-clamp-1">{source.data.Path}</span>
	</div>
{/if}
