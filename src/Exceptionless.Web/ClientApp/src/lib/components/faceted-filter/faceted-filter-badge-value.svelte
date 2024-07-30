<script lang="ts">
    import type { Snippet } from 'svelte';
    import Badge from '$comp/ui/badge/badge.svelte';

    interface Props {
        children: Snippet;
    }

    let { children }: Props = $props();
</script>

<Badge variant="secondary" class="rounded-sm px-1 font-normal lg:hidden">
    <span class="max-w-24 truncate">{@render children()}</span>
</Badge>
<div class="hidden space-x-1 lg:flex">
    <Badge variant="secondary" class="rounded-sm px-1 font-normal">
        <span class="max-w-60 truncate">{@render children()}</span>
    </Badge>
</div>
