<script lang="ts">
    import { SseClient, type SseClientOptions } from './sse-client.svelte';

    interface Props {
        onClient: (client: SseClient) => void;
        options?: SseClientOptions;
    }

    let { onClient, options = undefined }: Props = $props();

    $effect(() => {
        const client = new SseClient('/api/v2/push', options ?? {});
        onClient(client);

        return () => {
            client.close();
        };
    });
</script>
