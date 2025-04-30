<script lang="ts">
    import type { Snippet } from 'svelte';
    import type { HTMLAttributes } from 'svelte/elements';

    interface Props extends HTMLAttributes<HTMLElement> {
        children: Snippet;
    }

    let { children, class: className, ...props }: Props = $props();
</script>

<nav class={['flex space-x-2 lg:flex-col lg:space-y-1 lg:space-x-0', className]} {...props}>
    {@render children()}
</nav>
