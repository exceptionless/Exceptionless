<script lang="ts">
    import type { HTMLAttributes } from 'svelte/elements';

    import { type WithElementRef } from '$lib/utils';

    let { children, class: className, ref = $bindable(null), ...restProps }: WithElementRef<HTMLAttributes<HTMLDivElement>> = $props();
</script>

<div bind:this={ref} data-slot="notification-description" class={['mt-1 justify-items-start gap-1 text-sm [&_p]:leading-relaxed', className]} {...restProps}>
    {@render children?.()}
</div>
