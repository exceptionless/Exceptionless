<script lang="ts">
	import { cn, type WithElementRef } from "$lib/utils.js";
	import type { HTMLAttributes } from "svelte/elements";

	let {
		ref = $bindable(null),
		class: className,
		children,
		...restProps
	}: WithElementRef<HTMLAttributes<HTMLDivElement>> = $props();
</script>

<!-- CUSTOM: MATCH EXCEPTIONLESS CHART CONTAINER APPEARANCE WITH SUBTLE THEMED BORDER + SHADOW. -->
<div
	bind:this={ref}
	data-slot="chart-shell"
	class={cn("bg-card text-card-foreground rounded-lg border border-[color-mix(in_oklab,var(--chart-1)_42%,transparent)] shadow-sm", className)}
	{...restProps}
>
	{@render children?.()}
</div>
