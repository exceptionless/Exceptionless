<script lang="ts">
	import type { WebSocketMessageType, WebSocketMessageValue } from '$lib/models/websocket';
	import { createEventDispatcher, onMount } from 'svelte';

	export let type: WebSocketMessageType;

	function onMessage({ detail }: CustomEvent<WebSocketMessageValue<WebSocketMessageType>>) {
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
