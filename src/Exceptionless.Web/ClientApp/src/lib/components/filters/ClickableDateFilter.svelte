<script lang="ts">
	import { DateFilter } from './filters';

	export let term: string;
	export let value: Date | string | undefined;

	const title = `Search ${term}:${value}`;

	function onSearchClick() {
		document.dispatchEvent(
			new CustomEvent('filter', {
				detail: new DateFilter(term, value)
			})
		);
	}
</script>

<button on:click|preventDefault={onSearchClick} {title}>
	<slot />
</button>
