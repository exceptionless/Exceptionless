<script lang="ts">
	import type { ComponentProps } from "svelte";
	import { cn } from "#lib/utils.ts";
	import { PaginationLink } from "./index.ts";
	import ChevronRightIcon from '@lucide/svelte/icons/chevron-right';

	type PaginationNextProps = ComponentProps<typeof PaginationLink>;

	let { class: className, ...restProps }: PaginationNextProps = $props();
</script>

<PaginationLink
	aria-label="Go to next page"
	size="default"
	class={cn("pr-1.5!", className)}
	{...restProps}
>
	<span class="cn-pagination-next-text hidden sm:block">Next</span>
	<ChevronRightIcon data-icon="inline-end" />
</PaginationLink>
