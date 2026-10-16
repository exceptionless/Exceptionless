<script lang="ts">
    import RefreshCw from '@lucide/svelte/icons/refresh-cw';

    import type { ButtonSize } from '#comp/ui/button/index.ts';

    import { Button } from '#comp/ui/button/index.ts';

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
