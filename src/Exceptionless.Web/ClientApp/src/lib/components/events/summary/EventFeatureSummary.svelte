<script lang="ts">
	import type { EventSummaryModel, SummaryModel, SummaryTemplateKeys } from '$lib/models/api';

	export const badgeClass: string = '';
	export const showBadge: boolean = false;
	export const showStatus: boolean = false;
	export let showType: boolean;
	export let summary: SummaryModel<SummaryTemplateKeys>;
	const source = summary as EventSummaryModel<'event-feature-summary'>;
</script>

<div>
	{#if showType}
		<strong>Feature:&nbsp;</strong>
	{/if}
	<a href="/event/{source.id}" class="inline line-clamp-2">{source.data.Source}</a>
</div>
