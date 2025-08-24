<script lang="ts">
    import type { HTMLAttributes } from 'svelte/elements';

    import { cn, type WithElementRef } from '$lib/utils';

    let { children, class: className, ref = $bindable(null), ...restProps }: WithElementRef<HTMLAttributes<HTMLDivElement>> = $props();
</script>

<div bind:this={ref} data-slot="notification-title" class={cn('col-start-2 line-clamp-1 min-h-4 font-medium tracking-tight', className)} {...restProps}>
    {@render children?.()}
</div>
