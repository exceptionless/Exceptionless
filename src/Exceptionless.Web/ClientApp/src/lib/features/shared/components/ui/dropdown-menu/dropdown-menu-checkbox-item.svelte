<script lang="ts">
	import { DropdownMenu as DropdownMenuPrimitive, type WithoutChild } from "bits-ui";
    import Check from '~icons/mdi/check';
    import Minus from '~icons/mdi/remove';
	import { cn } from "$lib/utils.js";

	let {
		ref = $bindable(null),
		class: className,
		children: childrenProp,
		checked = $bindable(false),
		indeterminate = $bindable(false),
		...restProps
	}: WithoutChild<DropdownMenuPrimitive.CheckboxItemProps> = $props();

	export { className as class };
</script>

<DropdownMenuPrimitive.CheckboxItem
	bind:ref
	bind:checked
	bind:indeterminate
	class={cn(
		"data-[highlighted]:bg-accent data-[highlighted]:text-accent-foreground relative flex cursor-default select-none items-center rounded-sm py-1.5 pl-8 pr-2 text-sm outline-none data-[disabled]:pointer-events-none data-[disabled]:opacity-50",
		className
	)}
	{...restProps}
>
	{#snippet children({ checked })}
		<span class="absolute left-2 flex size-3.5 items-center justify-center">
			{#if indeterminate}
				<Minus class="size-3.5" />
			{:else}
				<Check class={cn("size-3.5", !checked && "text-transparent")} />
			{/if}
		</span>
		{@render childrenProp?.({ checked, indeterminate })}
	{/snippet}
</DropdownMenuPrimitive.CheckboxItem>
