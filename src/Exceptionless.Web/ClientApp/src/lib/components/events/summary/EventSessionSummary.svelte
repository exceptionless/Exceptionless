<script lang="ts">
	import type { EventSummaryModel, SummaryModel, SummaryTemplateKeys } from '$lib/models/api';

	export let showType: boolean;
	export let summary: SummaryModel<SummaryTemplateKeys>;
	const source = summary as EventSummaryModel<'event-session-summary'>;
</script>

<div class="line-clamp-2">
	{#if showType}
		<strong>
			{#if source.data.Type === 'sessionend'}
				Session End
			{:else if source.data.Type === 'heartbeat'}
				Session Heartbeat
			{:else}
				Session
			{/if}
		</strong>:&nbsp;
	{/if}

	<a href="/event/{source.id}" class="inline">
		{#if source.data.Name || source.data.Identity || source.data.SessionId}
			{source.data.Name || source.data.Identity || source.data.SessionId}
			{#if source.data.Name && source.data.Identity}
				<span class="text-muted"> ({source.data.Identity})</span>
			{/if}
		{/if}
	</a>
</div>
