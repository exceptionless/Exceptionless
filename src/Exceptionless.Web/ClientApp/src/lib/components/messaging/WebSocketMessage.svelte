<script lang="ts">
    import type { WebSocketMessageType } from '$lib/models/websocket';
    import CustomEventMessage from './CustomEventMessage.svelte';
    interface Props {
        type: WebSocketMessageType;
        message: (message: unknown) => void;
    }

    let { ...props }: Props = $props();
</script>

<CustomEventMessage {...props}></CustomEventMessage>
