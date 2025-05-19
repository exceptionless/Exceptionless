<script lang="ts">
    import type { Snippet } from 'svelte';
    import type { HTMLAttributes } from 'svelte/elements';

    interface Props extends HTMLAttributes<HTMLElement> {
        children: Snippet;
        width?: 'default' | 'narrow' | 'wide';
    }

    let { children, class: className, width = 'default', ...props }: Props = $props();

    const widthClasses = {
        default: 'lg:w-1/5',
        narrow: 'lg:w-1/6',
        wide: 'lg:w-1/4'
    };
</script>

<aside class={['w-full shrink-0', widthClasses[width], className]} {...props}>
    {@render children()}
</aside>
