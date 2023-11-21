<script lang="ts">
	import { Button } from '$comp/ui/button';
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

<Button variant="link" on:click={onSearchClick} {title}>
	<slot />
</Button>
