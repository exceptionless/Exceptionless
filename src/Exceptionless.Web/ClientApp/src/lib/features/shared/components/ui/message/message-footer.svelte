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
	data-slot="message-footer"
	class={cn(
		"text-xs font-medium text-muted-foreground px-3 group-has-data-[variant=ghost]/message:px-0 flex max-w-full min-w-0 items-center group-data-[align=end]/message:justify-end",
		className
	)}
	{...restProps}
>
	{@render children?.()}
</div>
