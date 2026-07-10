<script lang="ts">
	import { cn, type WithElementRef } from "$lib/utils.js";
	import type { HTMLAttributes } from "svelte/elements";

	let {
		ref = $bindable(null),
		class: className,
		children,
		...restProps
	}: WithElementRef<HTMLAttributes<HTMLElement>> = $props();
</script>

<!-- CUSTOM: KEEP KBD KEY-STYLE COMMAND SHORTCUT RENDERING; THIS CHANGE WAS INTRODUCED IN THE 2680d3ddb UI POLISH COMMIT AND SHOULD NOT BE OVERWRITTEN BY SHADCN UPDATES. -->
<kbd
	bind:this={ref}
	data-slot="command-shortcut"
	class={cn(
		"border-border bg-muted text-muted-foreground group-data-selected/command-item:bg-background group-data-selected/command-item:text-foreground ml-auto h-5 w-fit min-w-5 gap-1 rounded-sm border px-1 font-sans text-xs font-medium [&_svg:not([class*='size-'])]:size-3 pointer-events-none inline-flex items-center justify-center select-none",
		className
	)}
	{...restProps}
>
	{@render children?.()}
</kbd>
