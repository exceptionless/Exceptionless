<script lang="ts">
    import Live from '$comp/Live.svelte';
    import { Button } from '$comp/ui/button';

    interface Props {
        canRefresh: boolean;
        refresh: () => Promise<void>;
    }

    let { canRefresh, refresh }: Props = $props();

    const refreshButtonTitle = $derived(
        canRefresh ? 'Data will automatically-refresh' : 'Refresh data which is not automatically-refreshing'
    );
</script>

{#if canRefresh}
    <div class="inline-flex h-6">
        <Live liveTitle={refreshButtonTitle} class="ml-3 size-3 motion-safe:animate-none" />
    </div>
{:else}
    <Button variant="ghost" size="icon" onclick={refresh} title={refreshButtonTitle}>
        <Live live={false} notLiveTitle={refreshButtonTitle} class="size-3 motion-safe:animate-none" />
    </Button>
{/if}
