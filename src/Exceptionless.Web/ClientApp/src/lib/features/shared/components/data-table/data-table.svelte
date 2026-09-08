<script lang="ts">
    import type { Snippet } from 'svelte';

    interface Props {
        children: Snippet;
    }

    let { children }: Props = $props();
</script>

<div class="flex flex-col gap-2" data-slot="data-table">
    {@render children()}
</div>

<style>
    :global([data-slot='data-table']:has(> [data-slot='data-table-footer'][data-variant='floating'])) {
        min-height: calc(100dvh - 4rem);
    }
</style>
