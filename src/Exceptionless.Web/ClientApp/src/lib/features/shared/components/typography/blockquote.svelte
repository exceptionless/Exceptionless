<script lang="ts">
    import type { HTMLAttributes } from 'svelte/elements';

    type Props = HTMLAttributes<Element>;

    let { children, class: className, ...props }: Props = $props();
</script>

<blockquote class={['mt-6 border-l-2 pl-6 italic', className]} {...props}>
    {#if children}
        {@render children()}
    {/if}
</blockquote>
