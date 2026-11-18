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
	data-slot="code-block-header"
	class={cn(
		"gap-2 px-3 pt-3 text-muted-foreground group-data-[variant=destructive]/code-block:text-destructive group-data-[size=sm]/code-block:px-2.5 group-data-[size=sm]/code-block:pt-2.5 flex min-w-0 shrink-0 items-center *:has-[>[data-slot=code-block-title]]:min-w-0",
		className
	)}
	{...restProps}
>
	{@render children?.()}
</div>
