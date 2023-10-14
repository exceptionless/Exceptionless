<script lang="ts">
	import type { TermFilter } from './filters';

	export let term: string;
	export let value: string | number | boolean | null | undefined;

	const title = `Search ${term}:${value}`;

	function onSearchClick() {
		document.dispatchEvent(
			new CustomEvent('filter', {
				detail: <TermFilter>{
					term,
					value
				},
				bubbles: true
			})
		);
	}
</script>

<button on:click|preventDefault={onSearchClick} {title}>
	<slot />
</button>
