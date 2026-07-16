<script lang="ts">
	import type { HTMLAttributes } from "svelte/elements";
	import { cn, type WithElementRef } from "$lib/utils.js";

	let {
		ref = $bindable(null),
		class: className,
		children,
		...restProps
	}: WithElementRef<HTMLAttributes<HTMLElement>> = $props();
</script>

<!-- CUSTOM: REDUCE VERTICAL HEADER PADDING TO ALLOW A MORE COMPACT SIDEBAR STACKING. -->
<div
	bind:this={ref}
	data-slot="sidebar-header"
	data-sidebar="header"
	class={cn("gap-2 px-2 py-1 flex flex-col", className)}
	{...restProps}
>
	{@render children?.()}
</div>
