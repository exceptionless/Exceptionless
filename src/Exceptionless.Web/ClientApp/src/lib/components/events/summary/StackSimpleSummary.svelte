<script lang="ts">
	import { Badge } from '$comp/ui/badge';
	import IconChevronRight from '~icons/mdi/chevron-right';
	import type { StackSummaryModel, SummaryModel, SummaryTemplateKeys } from '$lib/models/api';

	export let badgeClass: string;
	export let showBadge: boolean;
	export let summary: SummaryModel<SummaryTemplateKeys>;
	const source = summary as StackSummaryModel<'stack-simple-summary'>;
</script>

{#if showBadge}
	<Badge class={badgeClass}>
		{source.status}
	</Badge>
{/if}

<div class="line-clamp-2">
	<strong>
		<abbr title={source.data.TypeFullName}>{source.data.Type}</abbr>:
	</strong>

	<a href="app.stack/{source.id}" class="inline">
		{source.title}
	</a>
</div>

{#if source.data.Path}
	<div class="hidden ml-6 text-sm sm:block text-muted-foreground">
		<IconChevronRight class="inline" />
		<span class="inline line-clamp-1">{source.data.Path}</span>
	</div>
{/if}
