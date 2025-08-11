<script lang="ts">
	import { cn, type WithElementRef } from "$lib/utils.js";
	import type { HTMLAttributes } from "svelte/elements";
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
	data-slot="table-row"
	class={cn(
		"hover:[&,&>svelte-css-wrapper]:[&>th,td]:bg-muted/50 data-[state=selected]:bg-muted border-b transition-colors",
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
	data-slot="table-row"
	class={cn(
		"hover:[&,&>svelte-css-wrapper]:[&>th,td]:bg-muted/50 data-[state=selected]:bg-muted border-b transition-colors",
		className
	)}
	{...restProps}
>
	{@render children?.()}
</tr>
{/if}
