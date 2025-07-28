<script lang="ts" module>
	import { type VariantProps, tv } from "tailwind-variants";

	export const alertVariants = tv({
		base: "relative grid w-full grid-cols-[0_1fr] items-start gap-y-0.5 rounded-lg border px-4 py-3 text-sm has-[>svg]:grid-cols-[calc(var(--spacing)*4)_1fr] has-[>svg]:gap-x-3 [&>svg]:size-4 [&>svg]:translate-y-0.5 [&>svg]:text-current",
		variants: {
			variant: {
				default: "bg-card text-card-foreground",
				destructive:
					"text-destructive bg-card *:data-[slot=alert-description]:text-destructive/90 [&>svg]:text-current",
				information:
					"border-blue-200 bg-blue-50 text-blue-900 dark:border-blue-900/30 dark:bg-blue-900/10 dark:text-blue-100 *:data-[slot=alert-description]:text-blue-800 dark:*:data-[slot=alert-description]:text-blue-200 [&>svg]:text-blue-600 dark:[&>svg]:text-blue-400",
				success:
					"border-green-200 bg-green-50 text-green-900 dark:border-green-900/30 dark:bg-green-900/10 dark:text-green-100 *:data-[slot=alert-description]:text-green-800 dark:*:data-[slot=alert-description]:text-green-200 [&>svg]:text-green-600 dark:[&>svg]:text-green-400",
			},
		},
		defaultVariants: {
			variant: "default",
		},
	});

	export type AlertVariant = VariantProps<typeof alertVariants>["variant"];
</script>

<script lang="ts">
	import type { HTMLAttributes } from "svelte/elements";
	import { cn, type WithElementRef } from "$lib/utils.js";

	let {
		ref = $bindable(null),
		class: className,
		variant = "default",
		children,
		...restProps
	}: WithElementRef<HTMLAttributes<HTMLDivElement>> & {
		variant?: AlertVariant;
	} = $props();
</script>

<div
	bind:this={ref}
	data-slot="alert"
	class={cn(alertVariants({ variant }), className)}
	{...restProps}
	role="alert"
>
	{@render children?.()}
</div>
