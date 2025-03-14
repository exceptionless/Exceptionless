<script lang="ts">
    import type { Snippet } from 'svelte';
    import type { HTMLAttributes } from 'svelte/elements';

    interface Props extends HTMLAttributes<HTMLDivElement> {
        children: Snippet;
        class?: string;
    }

    let { children, class: className, ...props }: Props = $props();
</script>

<div class={['flex-1', className]} {...props}>
    {@render children()}
</div>
