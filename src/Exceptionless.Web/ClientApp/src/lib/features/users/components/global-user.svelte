<script lang="ts">
    import type { Snippet } from 'svelte';

    import { getMeQuery } from '$features/users/api.svelte';

    let {
        children,
        disabled,
        loading
    }: {
        children: Snippet;
        disabled?: Snippet;
        loading?: Snippet;
    } = $props();

    const userQuery = getMeQuery();
    const hasGlobalRole = $derived(userQuery.data?.roles?.includes('global') ?? false);
</script>

{#if userQuery.isLoading}
    {@render loading?.()}
{:else if hasGlobalRole}
    {@render children()}
{:else}
    {@render disabled?.()}
{/if}
