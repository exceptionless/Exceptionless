<script lang="ts">
    import type { Snippet } from 'svelte';
    import type { HTMLAttributes } from 'svelte/elements';
    import { cn } from '$lib/utils';

    type Props = HTMLAttributes<Element> & {
        children?: Snippet<[unknown]>;
        items: unknown[];
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
