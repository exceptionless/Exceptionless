<script lang="ts">
	import { Badge } from '$comp/ui/badge';
	import IconChevronRight from '~icons/mdi/chevron-right';
	import type { StackSummaryModel, SummaryModel, SummaryTemplateKeys } from '$lib/models/api';
	import Muted from '$comp/typography/Muted.svelte';
	import A from '$comp/typography/A.svelte';

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

	<A href="/stack/{source.id}" class="inline">
		{source.title}
	</A>
</div>

{#if source.data.Path}
	<Muted class="hidden ml-6 sm:block">
		<IconChevronRight class="inline" />
		<span class="inline line-clamp-1">{source.data.Path}</span>
	</Muted>
{/if}
