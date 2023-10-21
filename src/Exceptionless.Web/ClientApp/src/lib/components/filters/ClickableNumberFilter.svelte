<script lang="ts">
	import { NumberFilter } from './filters';

	export let term: string;
	export let value: number | undefined;

	const title = `Search ${term}:${value}`;

	function onSearchClick() {
		document.dispatchEvent(
			new CustomEvent('filter', {
				detail: new NumberFilter(term, value)
			})
		);
	}
</script>

<button on:click|preventDefault={onSearchClick} {title}>
	<slot />
</button>
