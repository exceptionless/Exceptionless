<script lang="ts">
	import type { EventSummaryModel, SummaryModel, SummaryTemplateKeys } from '$lib/models/api';

	export let showType: boolean;
	export let summary: SummaryModel<SummaryTemplateKeys>;
	const source = summary as EventSummaryModel<'event-notfound-summary'>;
</script>

<div class="line-clamp-2">
	{#if showType}
		<strong>404</strong>:&nbsp;
	{/if}
	<a href="/event/{source.id}" class="inline">{source.data.Source}</a>
</div>
