<script lang="ts">
	import NumberFormatter from '../formatters/NumberFormatter.svelte';

	export let page: number;
	export let pageSize: number;
	export let pageTotal: number;
	export let total: number;

	$: start = page * pageSize + 1;
	$: end = start + pageTotal - 1;
</script>

{#if pageTotal !== 0 && total !== 0}
	<p class="text-sm text-gray-700">
		Showing
		<span class="font-medium"> <NumberFormatter value={start}></NumberFormatter></span>
		to
		<span class="font-medium">
			<NumberFormatter value={end}></NumberFormatter>
		</span>
		of
		<span class="font-medium">
			<NumberFormatter value={total}></NumberFormatter>
		</span>
		results
	</p>
{/if}
