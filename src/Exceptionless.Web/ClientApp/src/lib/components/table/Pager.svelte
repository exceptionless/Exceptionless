<script lang="ts">
	import { createEventDispatcher } from 'svelte';

	export let hasPrevious: boolean;
	export let hasNext: boolean;

	const dispatch = createEventDispatcher();
</script>

<div class="join">
	<button
		class="btn btn-square btn-outline join-item btn-sm"
		disabled={!hasPrevious}
		on:click|preventDefault={() => dispatch('previous')}>&lt;</button
	>
	<button
		class="btn btn-square btn-outline join-item btn-sm"
		disabled={!hasNext}
		on:click|preventDefault={() => dispatch('next')}>&gt;</button
	>
</div>
