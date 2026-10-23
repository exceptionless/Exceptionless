<script lang="ts">
	import * as Collapsible from "#comp/ui/collapsible/index.ts";
	import { cn, type WithoutChild } from "#lib/utils.ts";
	import type { Collapsible as CollapsiblePrimitive } from "bits-ui";

	let {
		ref = $bindable(null),
		class: className,
		children,
		...restProps
	}: WithoutChild<CollapsiblePrimitive.ContentProps> = $props();
</script>

<Collapsible.Content
	bind:ref
	data-slot="trace-collapsible-content"
	class="duration-200 min-w-0 overflow-hidden [--bits-accordion-content-height:var(--bits-collapsible-content-height)] data-open:animate-accordion-down data-closed:animate-accordion-up"
	{...restProps}
>
	<div
		data-slot="trace-collapsible-content-inner"
		class={cn(
			"mt-2 mb-1 ml-[calc(--spacing(2)_-_1px)] border-l pr-1 pl-4 leading-relaxed min-w-0 wrap-break-word text-muted-foreground",
			className
		)}
	>
		{@render children?.()}
	</div>
</Collapsible.Content>
