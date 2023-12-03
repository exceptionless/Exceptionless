<script lang="ts">
	import { getPageEnd, getPageStart } from '$lib/helpers/api';
	import NumberFormatter from '../formatters/Number.svelte';

	export let page: number;
	export let pageTotal: number;
	export let limit: number;
	export let total: number;

	$: start = getPageStart(page, limit);
	$: end = Math.min(getPageEnd(page, pageTotal, limit), total);
</script>

{#if pageTotal !== 0 && total !== 0}
	<p class="text-sm">
		<span class="text-muted-foreground">Showing</span>
		<span class="font-bold"><NumberFormatter value={start}></NumberFormatter></span>-<span
			class="font-bold"
		>
			<NumberFormatter value={end}></NumberFormatter>
		</span>
		<span class="text-muted-foreground">of</span>
		<span class="font-bold">
			<NumberFormatter value={total}></NumberFormatter>
		</span>
	</p>
{/if}
