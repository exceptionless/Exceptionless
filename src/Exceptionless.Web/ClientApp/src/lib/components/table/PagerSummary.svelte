<script lang="ts">
	import Muted from '$comp/typography/Muted.svelte';
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
	<Muted>
		Showing
		<span class="font-bold"><NumberFormatter value={start}></NumberFormatter></span>-<span
			class="font-bold"
		>
			<NumberFormatter value={end}></NumberFormatter>
		</span>
		of
		<span class="font-bold">
			<NumberFormatter value={total}></NumberFormatter>
		</span>
	</Muted>
{/if}
