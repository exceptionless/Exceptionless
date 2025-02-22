<script lang="ts">
    import type { HTMLAttributes } from 'svelte/elements';

    import { cn } from '$lib/utils';

    type Props = HTMLAttributes<Element> & {
        live?: boolean;
        liveTitle?: string;
        notLiveTitle?: string;
    };

    let { class: className, live = true, liveTitle = 'Live', notLiveTitle = 'Not Live' }: Props = $props();
</script>

{#if live}
    <span class={cn('inline-flex size-2 items-center rounded-full bg-green-500 motion-safe:animate-pulse', className)} title={liveTitle}></span>
{:else}
    <span class={cn('bg-destructive inline-flex size-2 items-center rounded-full', className)} title={notLiveTitle}></span>
{/if}
