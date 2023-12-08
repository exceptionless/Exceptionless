<script lang="ts">
	import A from '$comp/typography/A.svelte';
	import { DateFilter } from './filters';

	export let term: string;
	export let value: Date | string | undefined;

	const title = `Search ${term}:${value}`;

	function onSearchClick(e: Event) {
		e.preventDefault();
		document.dispatchEvent(
			new CustomEvent('filter', {
				detail: new DateFilter(term, value)
			})
		);
	}
</script>

<A on:click={onSearchClick} {title}>
	<slot />
</A>
