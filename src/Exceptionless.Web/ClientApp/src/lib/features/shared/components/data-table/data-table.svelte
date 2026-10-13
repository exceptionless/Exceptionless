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
