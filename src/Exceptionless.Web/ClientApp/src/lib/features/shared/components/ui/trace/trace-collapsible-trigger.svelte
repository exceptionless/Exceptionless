<script lang="ts">
	import * as Collapsible from "#comp/ui/collapsible/index.ts";
	import ChevronRightIcon from '@lucide/svelte/icons/chevron-right';
	import { cn, type WithoutChild } from "#lib/utils.ts";
	import TraceIcon from "./trace-icon.svelte";
	import type { Collapsible as CollapsiblePrimitive } from "bits-ui";

	let {
		ref = $bindable(null),
		class: className,
		children,
		...restProps
	}: WithoutChild<CollapsiblePrimitive.TriggerProps> = $props();
</script>

<Collapsible.Trigger
	bind:ref
	data-slot="trace-collapsible-trigger"
	class={cn(
		"gap-1 rounded-md focus-visible:ring-3 focus-visible:ring-ring/50 group/trace-collapsible-trigger flex w-fit max-w-full min-w-0 cursor-pointer items-center text-left text-muted-foreground transition-colors outline-none hover:text-foreground has-[[data-status=error]]:text-destructive",
		className
	)}
	{...restProps}
>
	{@render children?.()}
	<TraceIcon class="transition-transform group-aria-expanded/trace-collapsible-trigger:rotate-90">
		<ChevronRightIcon  />
	</TraceIcon>
</Collapsible.Trigger>
