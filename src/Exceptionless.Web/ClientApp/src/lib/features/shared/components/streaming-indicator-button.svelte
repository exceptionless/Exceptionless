<script lang="ts">
    import type { ButtonSize } from '$comp/ui/button';

    import { Button } from '$comp/ui/button';
    import Pause from '@lucide/svelte/icons/pause';
    import Play from '@lucide/svelte/icons/play';

    interface Props {
        onToggle: () => void;
        paused: boolean;
        size?: ButtonSize;
    }

    let { onToggle, paused, size = 'icon' }: Props = $props();

    const title = $derived(paused ? 'Resume streaming updates' : 'Pause streaming updates');
</script>

<Button variant="outline" {size} onclick={onToggle} {title}>
    {#if paused}
        <Play class="text-primary fill-primary size-4" />
    {:else}
        <Pause class="size-4" />
    {/if}
</Button>
