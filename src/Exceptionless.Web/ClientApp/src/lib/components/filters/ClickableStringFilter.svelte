<script lang="ts">
	import { StringFilter } from './filters';

	export let term: string;
	export let value: string | null | undefined;

	const title = `Search ${term}:${value}`;

	function onSearchClick() {
		document.dispatchEvent(
			new CustomEvent('filter', {
				detail: new StringFilter(term, value)
			})
		);
	}
</script>

<button on:click|preventDefault={onSearchClick} {title}>
	<slot />
</button>
