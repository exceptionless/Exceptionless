<script lang="ts">
	import { Button } from '$comp/ui/button';
	import { BooleanFilter } from './filters';

	export let term: string;
	export let value: boolean | undefined;

	const title = `Search ${term}:${value}`;

	function onSearchClick(e: Event) {
		e.preventDefault();
		document.dispatchEvent(
			new CustomEvent('filter', {
				detail: new BooleanFilter(term, value)
			})
		);
	}
</script>

<Button on:click={onSearchClick} {title}>
	<slot />
</Button>
