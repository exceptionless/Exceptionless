<script lang="ts">
	import { SessionFilter } from './filters';

	export let sessionId: string;

	const title = `Search ref.session:${sessionId}`;

	function onSearchClick() {
		document.dispatchEvent(
			new CustomEvent('filter', {
				detail: new SessionFilter(sessionId)
			})
		);
	}
</script>

<button on:click|preventDefault={onSearchClick} {title}>
	<slot />
</button>
