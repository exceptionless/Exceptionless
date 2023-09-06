<script lang="ts">
	import type { EventSummaryModel, SummaryModel, SummaryTemplateKeys } from '$lib/models/api';

	export const badgeClass: string = '';
	export const showBadge: boolean = false;
	export const showStatus: boolean = false;
	export let showType: boolean;
	export let summary: SummaryModel<SummaryTemplateKeys>;
	const source = summary as EventSummaryModel<'event-summary'>;
</script>

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
<a href="app.event/{source.id}" class="inline line-clamp-2">{source.data.Message}</a>
