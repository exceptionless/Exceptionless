<script lang="ts">
    import type { Snippet } from 'svelte';
    import Badge from '$comp/ui/badge/badge.svelte';

    interface Props {
        values: unknown[];
        displayValue: Snippet<[unknown]>;
    }

    let { values, displayValue }: Props = $props();
</script>

{#if values.length > 0}
    <Badge variant="secondary" class="rounded-sm px-1 font-normal lg:hidden">
        {values.length}
    </Badge>
    <div class="hidden space-x-1 lg:flex">
        {#if values.length > 2}
            <Badge variant="secondary" class="rounded-sm px-1 font-normal">
                {values.length} Selected
            </Badge>
        {:else}
            {#each values as value (value)}
                <Badge variant="secondary" class="rounded-sm px-1 font-normal">
                    <span class="max-w-14 truncate">{@render displayValue(value)}</span>
                </Badge>
            {/each}
        {/if}
    </div>
{/if}
