<script lang="ts">
	import { Button } from '$comp/ui/button';
	import { SessionFilter } from './filters';

	export let sessionId: string;

	const title = `Search ref.session:${sessionId}`;

	function onSearchClick(e: Event) {
		e.preventDefault();
		document.dispatchEvent(
			new CustomEvent('filter', {
				detail: new SessionFilter(sessionId)
			})
		);
	}
</script>

<Button on:click={onSearchClick} {title}>
	<slot />
</Button>
