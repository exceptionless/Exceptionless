<script lang="ts">
    import type { WebSocketMessageType } from '$lib/models/websocket';
    import CustomEventMessage from './CustomEventMessage.svelte';
    interface Props {
        type: WebSocketMessageType;
    }

    let { type }: Props = $props();
</script>

<CustomEventMessage {type} on:message></CustomEventMessage>
