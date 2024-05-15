<script lang="ts">
    import type { Snippet } from 'svelte';
    import type { HTMLAttributes } from 'svelte/elements';
    import { cn } from '$lib/utils';

    type Props = HTMLAttributes<Element> & {
        children: Snippet;
    };

    let { children, class: className, ...props }: Props = $props();
</script>

<code class={cn('relative rounded bg-muted px-[0.3rem] py-[0.2rem] font-mono text-sm font-semibold', className)} {...props}>
    {@render children()}
</code>
