<script lang="ts">
	import { Avatar as AvatarPrimitive } from "bits-ui";
	import { cn } from "$lib/utils.js";

	let {
		ref = $bindable(null),
		class: className,
		...restProps
	}: AvatarPrimitive.ImageProps = $props();
</script>

<AvatarPrimitive.Image
	bind:ref
	data-slot="avatar-image"
	class={cn("group-data-[shape=circle]/avatar:rounded-full group-data-[shape=square]/avatar:rounded-lg aspect-square size-full object-cover", className)}
	{...restProps}
/>
