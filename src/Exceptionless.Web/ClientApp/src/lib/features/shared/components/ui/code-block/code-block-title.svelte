<script lang="ts">
	import { cn, type WithElementRef } from "#lib/utils.ts";
	import type { HTMLAttributes } from "svelte/elements";

	let {
		ref = $bindable(null),
		class: className,
		children,
		...restProps
	}: WithElementRef<HTMLAttributes<HTMLDivElement>> = $props();
</script>

<div
	bind:this={ref}
	data-slot="code-block-title"
	class={cn(
		"font-sans text-sm font-medium text-foreground group-data-[variant=destructive]/code-block:text-destructive *:[svg]:me-1.5 [&_svg:not([class*='size-'])]:size-3.5 group-data-[size=sm]/code-block:text-xs min-w-0 truncate [&_svg]:pointer-events-none [&_svg]:inline-block [&_svg]:align-middle",
		className
	)}
	{...restProps}
>
	{@render children?.()}
</div>
