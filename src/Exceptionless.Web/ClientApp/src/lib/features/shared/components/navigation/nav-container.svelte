<script lang="ts">
    import type { Snippet } from 'svelte';
    import type { HTMLAttributes } from 'svelte/elements';

    interface Props extends HTMLAttributes<HTMLElement> {
        children: Snippet;
    }

    let { children, class: className, ...props }: Props = $props();
    // TODO: Look into having this use the sidbar menu items and a tab control on mobile.
</script>

<nav class={['flex space-x-2 lg:flex-col lg:space-y-1 lg:space-x-0', className]} {...props}>
    {@render children()}
</nav>
