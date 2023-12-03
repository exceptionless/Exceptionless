<script lang="ts">
	import FirstPageIcon from '~icons/mdi/first-page';
	import NavigateBeforeIcon from '~icons/mdi/navigate-before';
	import NavigateNextIcon from '~icons/mdi/navigate-next';
	import { createEventDispatcher } from 'svelte';
	import { Button } from '$comp/ui/button';

	export let canNavigateToFirstPage: boolean;
	export let hasPrevious: boolean;
	export let hasNext: boolean;

	const dispatch = createEventDispatcher();

	function navigateToFirstPage(e: Event) {
		e.preventDefault();
		dispatch('navigatetofirstpage');
	}

	function navigateToPreviousPage(e: Event) {
		e.preventDefault();
		dispatch('previous');
	}

	function navigateToNextPage(e: Event) {
		e.preventDefault();
		dispatch('next');
	}
</script>

{#if canNavigateToFirstPage}
	<Button on:click={navigateToFirstPage} variant="outline" size="icon"><FirstPageIcon /></Button>
{/if}
<Button disabled={!hasPrevious} on:click={navigateToPreviousPage} variant="outline" size="icon"
	><NavigateBeforeIcon /></Button
>
<Button disabled={!hasNext} on:click={navigateToNextPage} variant="outline" size="icon"
	><NavigateNextIcon /></Button
>
