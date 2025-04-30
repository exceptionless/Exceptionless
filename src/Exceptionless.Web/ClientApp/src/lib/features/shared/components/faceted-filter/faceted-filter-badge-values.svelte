<script lang="ts">
    import type { Snippet } from 'svelte';

    import Badge from '$comp/ui/badge/badge.svelte';

    interface Props {
        displayValue: Snippet<[unknown]>;
        values: unknown[];
    }

    let { displayValue, values }: Props = $props();
</script>

{#if values.length > 0}
    <Badge class="rounded-sm py-1.5 font-normal lg:hidden" variant="secondary">
        {values.length}
    </Badge>
    <div class="hidden space-x-1 lg:flex">
        {#if values.length > 2}
            <Badge class="rounded-sm py-1.5  font-normal" variant="secondary">
                {values.length} Selected
            </Badge>
        {:else}
            {#each values as value (value)}
                <Badge class="rounded-sm py-1.5 font-normal" variant="secondary">
                    <span class="max-w-24 truncate md:max-w-48">{@render displayValue(value)}</span>
                </Badge>
            {/each}
        {/if}
    </div>
{/if}
