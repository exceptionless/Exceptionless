<script lang="ts">
	import { createEventDispatcher, onMount } from 'svelte';

	export let type: string;

	function onMessage({ detail }: CustomEvent<unknown>) {
		dispatch('message', detail);
	}

	onMount(() => {
		document.addEventListener(type, onMessage);

		return () => {
			document.removeEventListener(type, onMessage);
		};
	});

	const dispatch = createEventDispatcher();
</script>
