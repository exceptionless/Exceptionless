<script lang="ts">
    import type { Snippet } from 'svelte';

    import Badge from '$comp/ui/badge/badge.svelte';

    interface Props {
        children: Snippet;
    }

    let { children }: Props = $props();
</script>

<Badge class="rounded-sm px-1 font-normal lg:hidden" variant="secondary">
    <span class="max-w-24 truncate md:max-w-48">{@render children()}</span>
</Badge>
<div class="hidden space-x-1 lg:flex">
    <Badge class="rounded-sm px-1 font-normal" variant="secondary">
        <span class="max-w-60 truncate md:max-w-120">{@render children()}</span>
    </Badge>
</div>
