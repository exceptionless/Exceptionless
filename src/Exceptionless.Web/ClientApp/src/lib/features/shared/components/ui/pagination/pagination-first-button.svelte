<script lang="ts">
	import { Pagination as PaginationPrimitive } from "bits-ui";
	import ChevronsLeftIcon from "@lucide/svelte/icons/chevrons-left";

	import { buttonVariants } from "$comp/ui/button/index.js";

	type Props = Omit<PaginationPrimitive.PageProps, "page"> &
		Partial<Pick<PaginationPrimitive.PageProps, "page">> & {
			currentPage?: number;
			showFromPage?: number;
		};

	let {
		ref = $bindable(null),
		class: className,
		children,
			page = { type: "page", value: 1 },
			currentPage = 1,
			showFromPage = 3,
			...restProps
		}: Props = $props();

		const show = $derived(currentPage >= showFromPage);
		const computedClass = $derived([
				buttonVariants({
					size: "default",
					variant: "ghost",
					class: "gap-1 px-2.5 sm:ps-2.5",
				}),
				!show && "invisible pointer-events-none",
				className
            ]
		);
		const ariaHidden = $derived(show ? undefined : true);
	</script>

{#snippet Fallback()}
	<ChevronsLeftIcon class="size-4" />
	<span class="sr-only">Go to first page</span>
{/snippet}

<PaginationPrimitive.Page
	bind:ref
	aria-label="Go to first page"
	aria-hidden={ariaHidden}
	class={computedClass}
	disabled={!show}
	children={children || Fallback}
	{page}
	{...restProps}
/>
