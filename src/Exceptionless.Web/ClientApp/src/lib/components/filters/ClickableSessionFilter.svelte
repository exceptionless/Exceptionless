<script lang="ts">
	import A from '$comp/typography/A.svelte';
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

<A on:click={onSearchClick} {title}>
	<slot />
</A>
