<script lang="ts">
    import type { Snippet } from 'svelte';
    import type { HTMLAttributes } from 'svelte/elements';
    import { cn } from '$lib/utils';

    type Props = HTMLAttributes<Element> & {
        children: Snippet;
    };

    let { children, class: className, ...props }: Props = $props();
</script>

<h1 class={cn('scroll-m-20 text-4xl font-extrabold tracking-tight lg:text-5xl', className)} {...props}>
    {@render children()}
</h1>
