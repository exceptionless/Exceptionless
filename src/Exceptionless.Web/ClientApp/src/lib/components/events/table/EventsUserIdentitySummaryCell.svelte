<script lang="ts">
	import type { SummaryModel, SummaryTemplateKeys, EventSummaryModel } from '$lib/models/api';

	export let summary: SummaryModel<SummaryTemplateKeys>;
	const source = summary as EventSummaryModel<SummaryTemplateKeys>;

	const identity =
		'Identity' in source.data && source.data.Identity ? (source.data.Identity as string) : null;
	const name = 'Name' in source.data && source.data.Name ? (source.data.Name as string) : null;
</script>

{#if name && identity}
	<abbr title="{name} ({identity})" class="line-clamp-1">
		{name}
	</abbr>
{:else if name || identity}
	<span class="line-clamp-1">
		{name || identity}
	</span>
{/if}
