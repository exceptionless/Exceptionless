<script lang="ts">
    import type { HTMLAttributes } from 'svelte/elements';

    import { cn } from '$lib/utils';

    type Props = HTMLAttributes<Element>;

    let { children, class: className, ...props }: Props = $props();
</script>

<small class={cn('text-sm leading-none font-medium', className)} {...props}>
    {#if children}
        {@render children()}
    {/if}
</small>
