<script lang="ts">
    import type { HTMLAttributes } from 'svelte/elements';

    import { cn } from '$lib/utils';

    type Props = HTMLAttributes<Element>;

    let { children, class: className, ...props }: Props = $props();
</script>

<div class={cn('text-lg font-semibold', className)} {...props}>
    {#if children}
        {@render children()}
    {/if}
</div>
