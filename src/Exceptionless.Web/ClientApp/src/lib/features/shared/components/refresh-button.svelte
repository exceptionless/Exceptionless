<script lang="ts">
    import { Button } from '$comp/ui/button';
    import RefreshCw from '@lucide/svelte/icons/refresh-cw';

    interface Props {
        isRefreshing?: boolean;
        onRefresh: () => void;
        title?: string;
    }

    let { isRefreshing = false, onRefresh, title = 'Refresh results' }: Props = $props();
</script>

<Button variant="outline" size="icon" onclick={onRefresh} {title}>
    <RefreshCw class={`size-4 ${isRefreshing ? 'animate-spin' : ''}`} />
</Button>
