<script lang="ts">
	import A from '$comp/typography/A.svelte';
	import { VersionFilter } from './filters';

	export let term: string;
	export let value: string | undefined;

	const title = `Search ${term}:${value}`;

	function onSearchClick(e: Event) {
		e.preventDefault();
		document.dispatchEvent(
			new CustomEvent('filter', {
				detail: new VersionFilter(term, value)
			})
		);
	}
</script>

<A on:click={onSearchClick} {title}>
	<slot />
</A>
