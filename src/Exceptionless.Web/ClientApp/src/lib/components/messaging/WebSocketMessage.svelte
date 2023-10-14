<script lang="ts">
	import type { WebSocketMessageType } from '$lib/models/websocket';
	import CustomEventMessage from './CustomEventMessage.svelte';

	export let type: WebSocketMessageType;
</script>

<CustomEventMessage {type} on:message}></CustomEventMessage>
