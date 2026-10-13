<script lang="ts">
    import { type QueryClient, QueryClientProvider } from '@tanstack/svelte-query';

    import QueryGateObserver, { type QueryGate } from './query-gating.test-observer.svelte';

    let { client, enabled, gate }: { client: QueryClient; enabled: boolean; gate: QueryGate } = $props();
</script>

<QueryClientProvider {client}>
    <QueryGateObserver {enabled} {gate} />
</QueryClientProvider>
