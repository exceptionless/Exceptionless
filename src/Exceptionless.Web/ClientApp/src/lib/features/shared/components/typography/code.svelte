<script lang="ts">
    import type { HTMLAttributes } from 'svelte/elements';

    type Props = HTMLAttributes<Element>;

    let { children, class: className, ...props }: Props = $props();
</script>

<code class={['bg-muted relative rounded px-[0.3rem] py-[0.2rem] font-mono text-sm', className]} {...props}>
    {#if children}
        {@render children()}
    {/if}
</code>
