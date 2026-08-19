<script lang="ts">
	import { Avatar as AvatarPrimitive } from "bits-ui";
	import { cn } from "$lib/utils.js";

	let {
		ref = $bindable(null),
		loadingStatus = $bindable("loading"),
		shape = "circle",
		size = "default",
		class: className,
		...restProps
	}: AvatarPrimitive.RootProps & {
		shape?: "circle" | "square";
		size?: "default" | "sm" | "lg";
	} = $props();
</script>

<AvatarPrimitive.Root
	bind:ref
	bind:loadingStatus
	data-slot="avatar"
	data-shape={shape}
	data-size={size}
	class={cn(
		"size-8 data-[shape=circle]:rounded-full data-[shape=circle]:after:rounded-full data-[shape=square]:rounded-lg data-[shape=square]:after:rounded-lg data-[size=lg]:size-10 data-[size=sm]:size-6 after:border-border group/avatar relative flex shrink-0 select-none after:absolute after:inset-0 after:border after:mix-blend-darken dark:after:mix-blend-lighten",
		className
	)}
	{...restProps}
/>
