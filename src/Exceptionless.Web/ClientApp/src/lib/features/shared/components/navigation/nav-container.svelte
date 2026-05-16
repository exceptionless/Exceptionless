<script lang="ts">
    import type { Snippet } from 'svelte';
    import type { HTMLAttributes } from 'svelte/elements';

    interface Props extends HTMLAttributes<HTMLElement> {
        children: Snippet;
        orientation?: 'horizontal' | 'responsive';
    }

    let { children, class: className, orientation = 'responsive', ...props }: Props = $props();
    // TODO: Look into having this use the sidbar menu items and a tab control on mobile.
</script>

<nav
    class={[
        'flex',
        orientation === 'horizontal' ? 'gap-2 overflow-x-auto pb-1' : 'space-x-2 lg:flex-col lg:space-y-1 lg:space-x-0',
        className
    ]}
    {...props}
>
    {@render children()}
</nav>
