<script lang="ts">
	import A from '$comp/typography/A.svelte';
	import { ReferenceFilter } from './filters';

	export let referenceId: string;

	const title = `Search reference:${referenceId}`;

	function onSearchClick(e: Event) {
		e.preventDefault();
		document.dispatchEvent(
			new CustomEvent('filter', {
				detail: new ReferenceFilter(referenceId)
			})
		);
	}
</script>

<A on:click={onSearchClick} {title}>
	<slot />
</A>
