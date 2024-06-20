<script lang="ts" context="module">
    type TData = unknown;
</script>

<script lang="ts" generics="TData">
    import type { Snippet } from 'svelte';
    import type { HTMLAttributes } from 'svelte/elements';
    import { cn } from '$lib/utils';

    type Props = HTMLAttributes<Element> & {
        children?: Snippet<[TData]>;
        items: TData[];
    };

    let { children, class: className, items = [], ...props }: Props = $props();
</script>

<ul class={cn('my-6 ml-6 list-disc [&>li]:mt-2', className)} {...props}>
    {#each items as item}
        <li>
            {#if children}{@render children(item)}{:else}{item}{/if}
        </li>
    {/each}
</ul>
