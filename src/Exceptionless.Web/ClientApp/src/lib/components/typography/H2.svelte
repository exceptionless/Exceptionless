<script lang="ts">
    import type { HTMLAttributes } from 'svelte/elements';

    import { cn } from '$lib/utils';

    type Props = HTMLAttributes<Element>;

    let { children, class: className, ...props }: Props = $props();
</script>

<h2 class={cn('scroll-m-20 border-b pb-2 text-3xl font-semibold tracking-tight transition-colors first:mt-0', className)} {...props}>
    {#if children}
        {@render children()}
    {/if}
</h2>
