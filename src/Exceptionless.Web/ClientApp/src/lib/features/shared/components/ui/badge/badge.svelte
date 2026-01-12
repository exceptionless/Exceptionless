<script lang="ts" module>
	import { type VariantProps, tv } from "tailwind-variants";

	export const badgeVariants = tv({
		base: "focus-visible:border-ring focus-visible:ring-ring/50 aria-invalid:ring-destructive/20 dark:aria-invalid:ring-destructive/40 aria-invalid:border-destructive inline-flex w-fit shrink-0 items-center justify-center gap-1 overflow-hidden rounded-full border px-2 py-0.5 text-xs font-medium whitespace-nowrap transition-[color,box-shadow] focus-visible:ring-[3px] [&>svg]:pointer-events-none [&>svg]:size-3",
		variants: {
			variant: {
				default:
					"bg-primary text-primary-foreground [a&]:hover:bg-primary/90 border-transparent",
				secondary:
					"bg-secondary text-secondary-foreground [a&]:hover:bg-secondary/90 border-transparent",
				   destructive:
					   "bg-destructive [a&]:hover:bg-destructive/90 focus-visible:ring-destructive/20 dark:focus-visible:ring-destructive/40 dark:bg-destructive/70 border-transparent text-white",
				   outline: "text-foreground [a&]:hover:bg-accent [a&]:hover:text-accent-foreground",
				   red:
					   "bg-red-100 text-red-700 [a&]:hover:bg-red-200 border-transparent focus-visible:ring-red-400 dark:bg-red-900/30 dark:text-red-300 dark:[a&]:hover:bg-red-900/50",
				   amber:
					   "bg-amber-100 text-amber-700 [a&]:hover:bg-amber-200 border-transparent focus-visible:ring-amber-400 dark:bg-amber-900/30 dark:text-amber-300 dark:[a&]:hover:bg-amber-900/50",
				   orange:
					   "bg-orange-100 text-orange-700 [a&]:hover:bg-orange-200 border-transparent focus-visible:ring-orange-400 dark:bg-orange-900/30 dark:text-orange-300 dark:[a&]:hover:bg-orange-900/50",
			   },
		   },
		defaultVariants: {
			variant: "default",
		},
	});

	export type BadgeVariant = VariantProps<typeof badgeVariants>["variant"];
</script>

<script lang="ts">
	import type { HTMLAnchorAttributes } from "svelte/elements";
	import { cn, type WithElementRef } from "$lib/utils.js";

	let {
		ref = $bindable(null),
		href,
		class: className,
		variant = "default",
		children,
		...restProps
	}: WithElementRef<HTMLAnchorAttributes> & {
		variant?: BadgeVariant;
	} = $props();
</script>

<svelte:element
	this={href ? "a" : "span"}
	bind:this={ref}
	data-slot="badge"
	{href}
	class={cn(badgeVariants({ variant }), className)}
	{...restProps}
>
	{@render children?.()}
</svelte:element>
