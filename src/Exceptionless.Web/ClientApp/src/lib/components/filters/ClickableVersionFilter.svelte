<script lang="ts">
	import { VersionFilter } from './filters';

	export let term: string;
	export let value: string | undefined;

	const title = `Search ${term}:${value}`;

	function onSearchClick() {
		document.dispatchEvent(
			new CustomEvent('filter', {
				detail: new VersionFilter(term, value)
			})
		);
	}
</script>

<button on:click|preventDefault={onSearchClick} {title}>
	<slot />
</button>
