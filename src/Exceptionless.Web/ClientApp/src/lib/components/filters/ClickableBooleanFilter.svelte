<script lang="ts">
	import { BooleanFilter } from './filters';

	export let term: string;
	export let value: boolean | undefined;

	const title = `Search ${term}:${value}`;

	function onSearchClick() {
		document.dispatchEvent(
			new CustomEvent('filter', {
				detail: new BooleanFilter(term, value)
			})
		);
	}
</script>

<button on:click|preventDefault={onSearchClick} {title}>
	<slot />
</button>
