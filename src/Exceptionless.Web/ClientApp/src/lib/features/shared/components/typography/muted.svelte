<script lang="ts">
    import type { HTMLAttributes } from 'svelte/elements';

    import { cn } from '$lib/utils';

    type Props = HTMLAttributes<Element>;

    let { children, class: className, ...props }: Props = $props();
</script>

<p class={cn('text-sm text-muted-foreground', className)} {...props}>
    {#if children}
        {@render children()}
    {/if}
</p>
