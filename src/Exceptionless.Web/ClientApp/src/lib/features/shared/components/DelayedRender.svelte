<script lang="ts">
    import type { Snippet } from 'svelte';

    interface Props {
        children: Snippet;
        delay?: number;
        visible: boolean;
    }

    let { children, delay = 50, visible = true }: Props = $props();

    let shouldRender = $state(false);
    let timeout: ReturnType<typeof setTimeout>;

    $effect(() => {
        if (visible) {
            timeout = setTimeout(() => {
                shouldRender = true;
            }, delay);
        } else {
            shouldRender = false;
            clearTimeout(timeout);
        }

        return () => clearTimeout(timeout);
    });
</script>

{#if shouldRender && children}
    {@render children()}
{/if}
