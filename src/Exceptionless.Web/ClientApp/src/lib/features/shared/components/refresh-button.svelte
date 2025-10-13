<script lang="ts">
    import type { ButtonSize } from '$comp/ui/button';

    import { Button } from '$comp/ui/button';
    import RefreshCw from '@lucide/svelte/icons/refresh-cw';

    interface Props {
        isRefreshing?: boolean;
        onRefresh: () => void;
        size?: ButtonSize;
        title?: string;
    }

    let { isRefreshing = false, onRefresh, size = 'icon', title = 'Refresh results' }: Props = $props();
</script>

<Button variant="outline" {size} onclick={onRefresh} {title}>
    <RefreshCw class={`size-4 ${isRefreshing ? 'animate-spin' : ''}`} />
</Button>
