<script lang="ts">
	import * as Collapsible from "#comp/ui/collapsible/index.ts";
	import { cn } from "#lib/utils.ts";
	import type { Collapsible as CollapsiblePrimitive } from "bits-ui";

	let {
		ref = $bindable(null),
		open = $bindable(false),
		class: className,
		...restProps
	}: CollapsiblePrimitive.RootProps = $props();
</script>

<Collapsible.Root
	bind:ref
	bind:open
	data-slot="trace-collapsible"
	class={cn("flex w-full min-w-0 flex-col", className)}
	{...restProps}
/>
