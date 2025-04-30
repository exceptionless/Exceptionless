<script lang="ts">
    import type { HTMLAttributes } from 'svelte/elements';

    type Props = HTMLAttributes<Element>;

    let { children, class: className, ...props }: Props = $props();
</script>

<code class={['bg-muted relative rounded px-[0.6rem] py-[0.5rem] font-mono text-sm', className]} {...props}>
    {#if children}
        {@render children()}
    {/if}
</code>
