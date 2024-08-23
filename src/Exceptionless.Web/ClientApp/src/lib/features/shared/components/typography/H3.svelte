<script lang="ts">
    import type { HTMLAttributes } from 'svelte/elements';

    import { cn } from '$lib/utils';

    type Props = HTMLAttributes<Element>;

    let { children, class: className, ...props }: Props = $props();
</script>

<h3 class={cn('scroll-m-20 text-2xl font-semibold tracking-tight', className)} {...props}>
    {#if children}
        {@render children()}
    {/if}
</h3>
