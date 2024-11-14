<script lang="ts">
	import { ContextMenu as ContextMenuPrimitive, type WithoutChildrenOrChild } from "bits-ui";
    import Check from '~icons/mdi/check';
	import Minus from '~icons/mdi/remove';
	import type { Snippet } from "svelte";
	import { cn } from "$lib/utils.js";

	let {
		ref = $bindable(null),
		class: className,
		checked = $bindable(),
		indeterminate = $bindable(),
		children,
		...restProps
	}: WithoutChildrenOrChild<ContextMenuPrimitive.CheckboxItemProps> & {
		children?: Snippet;
	} = $props();
</script>

<ContextMenuPrimitive.CheckboxItem
	bind:checked
	bind:indeterminate
	bind:ref
	class={cn(
		"data-[highlighted]:bg-accent data-[highlighted]:text-accent-foreground relative flex cursor-default select-none items-center rounded-sm py-1.5 pl-8 pr-2 text-sm outline-none data-[disabled]:pointer-events-none data-[disabled]:opacity-50",
		className
	)}
	{...restProps}
>
	<span class="absolute left-2 flex size-3.5 items-center justify-center">
		{#if indeterminate}
			<Minus class="size-4" />
		{:else}
			<Check class={cn("size-4", !checked && "text-transparent")} />
		{/if}
	</span>
	{@render children?.()}
</ContextMenuPrimitive.CheckboxItem>
