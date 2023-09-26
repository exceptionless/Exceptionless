<script lang="ts">
	import { getPageEnd, getPageStart } from '$lib/helpers/api';
	import NumberFormatter from '../formatters/NumberFormatter.svelte';

	export let page: number;
	export let pageTotal: number;
	export let limit: number;
	export let total: number;

	$: start = getPageStart(page, limit);
	$: end = Math.min(getPageEnd(page, pageTotal, limit), total);
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
