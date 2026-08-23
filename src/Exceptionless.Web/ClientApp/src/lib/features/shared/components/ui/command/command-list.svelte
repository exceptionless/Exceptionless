<script lang="ts">
	import { Command as CommandPrimitive } from "bits-ui";
	import { cn } from "$lib/utils.js";

	let {
		ref = $bindable(null),
		class: className,
		...restProps
	}: CommandPrimitive.ListProps = $props();
</script>

<!-- CUSTOM: KEEP COMMAND PALETTE OVERFLOW VISUALLY DISCOVERABLE WITH A THIN SCROLLBAR. -->
<CommandPrimitive.List
	bind:ref
	data-slot="command-list"
	class={cn(
		"max-h-72 scroll-py-1 overflow-x-hidden overflow-y-auto outline-none [scrollbar-color:var(--muted-foreground)_transparent] [scrollbar-width:thin] [&::-webkit-scrollbar]:w-2 [&::-webkit-scrollbar-thumb]:rounded-full [&::-webkit-scrollbar-thumb]:bg-muted-foreground/50 [&::-webkit-scrollbar-track]:bg-transparent",
		className
	)}
	{...restProps}
/>
