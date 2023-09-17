<script lang="ts">
	import FirstPageIcon from '~icons/mdi/first-page';
	import NavigateBeforeIcon from '~icons/mdi/navigate-before';
	import NavigateNextIcon from '~icons/mdi/navigate-next';
	import { createEventDispatcher } from 'svelte';

	export let canNavigateToFirstPage: boolean;
	export let hasPrevious: boolean;
	export let hasNext: boolean;

	const dispatch = createEventDispatcher();
</script>

<div class="join">
    {#if canNavigateToFirstPage}
	<button
		class="btn btn-square btn-outline join-item btn-sm"
		on:click|preventDefault={() => dispatch('navigatetofirstpage')}><FirstPageIcon /></button
	>
    {/if}
	<button
		class="btn btn-square btn-outline join-item btn-sm"
		disabled={!hasPrevious}
		on:click|preventDefault={() => dispatch('previous')}><NavigateBeforeIcon /></button
	>
	<button
		class="btn btn-square btn-outline join-item btn-sm"
		disabled={!hasNext}
		on:click|preventDefault={() => dispatch('next')}><NavigateNextIcon /></button
	>
</div>
