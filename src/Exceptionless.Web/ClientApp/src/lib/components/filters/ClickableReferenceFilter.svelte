<script lang="ts">
	import { ReferenceFilter } from './filters';

	export let referenceId: string;

	const title = `Search reference:${referenceId}`;

	function onSearchClick() {
		document.dispatchEvent(
			new CustomEvent('filter', {
				detail: new ReferenceFilter(referenceId)
			})
		);
	}
</script>

<button on:click|preventDefault={onSearchClick} {title}>
	<slot />
</button>
