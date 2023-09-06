<script lang="ts">
	import IconChevronRight from '~icons/mdi/chevron-right';
	import type { StackSummaryModel, SummaryModel, SummaryTemplateKeys } from '$lib/models/api';

	export let badgeClass: string;
	export let showBadge: boolean;
	export const showStatus: boolean = false;
	export const showType: boolean = false;
	export let summary: SummaryModel<SummaryTemplateKeys>;
	const source = summary as StackSummaryModel<'stack-simple-summary'>;
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

	<a href="app.stack/{source.id}" class="inline line-clamp-2">
		{source.title}
	</a>
</div>

{#if source.data.Path}
	<div class="hidden sm:block text-gray-500 ml-6 text-sm">
		<IconChevronRight class="inline" />
		<span class="inline line-clamp-1">{source.data.Path}</span>
	</div>
{/if}
