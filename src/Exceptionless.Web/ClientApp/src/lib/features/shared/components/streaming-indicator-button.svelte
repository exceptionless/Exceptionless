<script lang="ts">
    import { Button } from '$comp/ui/button';
    import Play from '@lucide/svelte/icons/play';

    interface Props {
        onToggle: () => void;
        paused: boolean;
    }

    let { onToggle, paused }: Props = $props();

    const title = $derived(paused ? 'Resume streaming updates' : 'Pause streaming updates');
</script>

<Button variant="outline" size="icon" onclick={onToggle} {title}>
    {#if paused}
        <Play class="text-primary fill-primary size-4" />
    {:else}
        <span class="inline-flex size-2 animate-pulse items-center rounded-full bg-red-500"></span>
    {/if}
</Button>
