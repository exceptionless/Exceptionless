<script lang="ts">
	import A from '$comp/typography/A.svelte';
	import { Badge } from '$comp/ui/badge';
	import type { StackSummaryModel, SummaryModel, SummaryTemplateKeys } from '$lib/models/api';

	export let badgeClass: string;
	export let showBadge: boolean;
	export let showType: boolean;
	export let summary: SummaryModel<SummaryTemplateKeys>;
	const source = summary as StackSummaryModel<'stack-summary'>;
</script>

<div class="line-clamp-2">
	{#if showBadge}
		<Badge class={badgeClass}>
			{source.status}
		</Badge>
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

	<A class="inline">
		{source.title}
	</A>
</div>
