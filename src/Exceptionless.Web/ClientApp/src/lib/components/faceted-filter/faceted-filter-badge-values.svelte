<script lang="ts">
    import Badge from '$comp/ui/badge/badge.svelte';

    export let values: unknown[];
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
                    <span class="max-w-14 truncate"><slot {value} /></span>
                </Badge>
            {/each}
        {/if}
    </div>
{/if}
