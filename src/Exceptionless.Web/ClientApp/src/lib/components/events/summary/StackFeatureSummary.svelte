<script lang="ts">
	import { Badge } from '$comp/ui/badge';
	import type { StackSummaryModel, SummaryModel, SummaryTemplateKeys } from '$lib/models/api';

	export let badgeClass: string;
	export let showBadge: boolean;
	export let showType: boolean;
	export let summary: SummaryModel<SummaryTemplateKeys>;
	const source = summary as StackSummaryModel<'stack-feature-summary'>;
</script>

<div class="line-clamp-2">
	{#if showBadge}
		<Badge class={badgeClass}>
			{source.status}
		</Badge>
	{/if}

	{#if showType}
		<strong>Feature</strong>:&nbsp;
	{/if}

	<a href="/stack/{source.id}" class="inline">
		{source.title}
	</a>
</div>
