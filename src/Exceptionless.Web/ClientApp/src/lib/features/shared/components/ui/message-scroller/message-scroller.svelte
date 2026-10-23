<script lang="ts">
	import { MessageScroller as MessageScrollerPrimitive } from "@shadcn-svelte/primitives/message-scroller";
	import { cn } from "#lib/utils.ts";

	let {
		class: className,
		ref = $bindable(null),
		...restProps
	}: MessageScrollerPrimitive.RootProps = $props();
</script>

<MessageScrollerPrimitive.Root
	bind:ref
	data-slot="message-scroller"
	class={cn(
		"group/message-scroller relative flex size-full min-h-0 flex-col overflow-hidden",
		className
	)}
	{...restProps}
/>
