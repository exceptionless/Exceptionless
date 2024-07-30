<script context="module" lang="ts">
    type TData = unknown;
</script>

<script generics="TData" lang="ts">
    import type { Snippet } from 'svelte';
    import type { HTMLAttributes } from 'svelte/elements';

    import { cn } from '$lib/utils';

    type Props = {
        displayValue?: Snippet<[TData]>;
        items: TData[];
    } & HTMLAttributes<Element>;

    let { class: className, displayValue, items = [], ...props }: Props = $props();
</script>

<ul class={cn('my-6 ml-6 list-disc [&>li]:mt-2', className)} {...props}>
    {#each items as item}
        <li>
            {#if displayValue}{@render displayValue(item)}{:else}{item}{/if}
        </li>
    {/each}
</ul>
