<script lang="ts">
	import type { HTMLAttributes } from "svelte/elements";
	import type { WithElementRef } from "bits-ui";
	import { cn } from "$lib/utils.js";
    import { slide } from 'svelte/transition';

	let {
		ref = $bindable(null),
		class: className,
		children,
		...restProps
	}: WithElementRef<HTMLAttributes<HTMLTableRowElement>> = $props();

    const shouldAnimate = $derived(className?.includes('only:table-row') !== true);
</script>

{#if shouldAnimate}
    <tr
        bind:this={ref}
        class={cn(
            "hover:bg-muted/50 data-[state=selected]:bg-muted border-b transition-colors",
            className
        )}
        {...restProps}
        transition:slide={{ delay: 0, duration: 250, axis: 'y' }}
    >
        {@render children?.()}
    </tr>
{:else}
    <tr
        bind:this={ref}
        class={cn(
            "hover:bg-muted/50 data-[state=selected]:bg-muted border-b transition-colors",
            className
        )}
        {...restProps}
        >
        {@render children?.()}
    </tr>
{/if}
