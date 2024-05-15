<script lang="ts">
    import type { Snippet } from 'svelte';
    import type { HTMLAttributes } from 'svelte/elements';
    import { cn } from '$lib/utils';

    type Props = HTMLAttributes<Element> & {
        children: Snippet;
    };

    let { children, class: className, ...props }: Props = $props();
</script>

<blockquote class={cn('mt-6 border-l-2 pl-6 italic', className)} {...props}>
    {@render children()}
</blockquote>
