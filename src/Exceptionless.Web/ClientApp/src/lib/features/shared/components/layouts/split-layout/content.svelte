<script lang="ts">
    import type { Snippet } from 'svelte';
    import type { HTMLAttributes } from 'svelte/elements';

    interface Props extends HTMLAttributes<HTMLDivElement> {
        children: Snippet;
        class?: string;
    }

    let { children, class: className, ...props }: Props = $props();
</script>

<div class={['min-w-0 flex-1 overflow-x-auto', className]} {...props}>
    {@render children()}
</div>
