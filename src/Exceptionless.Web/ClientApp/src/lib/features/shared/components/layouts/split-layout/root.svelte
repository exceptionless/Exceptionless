<script lang="ts">
    import type { Snippet } from "svelte";
    import type { HTMLAttributes } from 'svelte/elements';

    import { cn } from '$lib/utils';

    interface Props extends HTMLAttributes<HTMLDivElement> {
        children: Snippet;
        class?: string;
    }

    let { children, class: className, ...props }: Props = $props();
</script>

<div
    class={cn(
        "flex flex-col space-y-8 lg:flex-row lg:space-y-0 lg:space-x-12",
        className
    )}
    {...props}
>
    {@render children()}
</div>
