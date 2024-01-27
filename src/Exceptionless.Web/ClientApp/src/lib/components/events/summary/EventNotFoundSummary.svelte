<script lang="ts">
	import A from '$comp/typography/A.svelte';
	import type { EventSummaryModel, SummaryModel, SummaryTemplateKeys } from '$lib/models/api';

	export let showType: boolean;
	export let summary: SummaryModel<SummaryTemplateKeys>;
	const source = summary as EventSummaryModel<'event-notfound-summary'>;
</script>

<div class="line-clamp-2">
	{#if showType}
		<strong>404</strong>:&nbsp;
	{/if}
	<A class="inline">{source.data.Source}</A>
</div>
