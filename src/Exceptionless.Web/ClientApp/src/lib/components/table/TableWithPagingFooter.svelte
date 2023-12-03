<script lang="ts">
	import ErrorMessage from '$comp/ErrorMessage.svelte';
	import Loading from '$comp/Loading.svelte';
	import {
		DEFAULT_LIMIT,
		canNavigateToFirstPage,
		hasPreviousPage,
		hasNextPage
	} from '$lib/helpers/api';
	import Pager from './Pager.svelte';
	import PagerSummary from './PagerSummary.svelte';

	export let loading: boolean;
	export let error: string[] | undefined;

	export let page: number;
	export let pageTotal: number;
	export let limit = DEFAULT_LIMIT;
	export let total: number;

	export let onNavigateToFirstPage: () => void;
	export let onPreviousPage: () => void;
	export let onNextPage: () => void;
</script>

<div class="flex items-center justify-between flex-1 text-sm text-muted-foreground">
	<div class="py-2">
		{#if loading}
			<Loading></Loading>
		{:else if error}
			<ErrorMessage message={error}></ErrorMessage>
		{/if}
	</div>

	{#if total}
		<PagerSummary {page} {pageTotal} {limit} {total}></PagerSummary>

		<div class="py-2">
			<Pager
				canNavigateToFirstPage={canNavigateToFirstPage(page)}
				on:navigatetofirstpage={() => onNavigateToFirstPage()}
				hasPrevious={hasPreviousPage(page)}
				on:previous={() => onPreviousPage()}
				hasNext={hasNextPage(page, pageTotal, limit, total)}
				on:next={() => onNextPage()}
			></Pager>
		</div>
	{/if}
</div>
