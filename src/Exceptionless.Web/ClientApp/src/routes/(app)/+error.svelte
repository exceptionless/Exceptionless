<script lang="ts">
    import { resolve } from '$app/paths';
    import { page } from '$app/state';
    import SearchXIcon from '@lucide/svelte/icons/search-x';
    import ShieldAlertIcon from '@lucide/svelte/icons/shield-alert';

    import { Button } from '#comp/ui/button/index.ts';
    import { getErrorMessage, getErrorTitle } from '#features/shared/error-page.ts';

    // Rendered inside the app layout, so a stale link leaves navigation and Exie's panel open.
    const isNotFound = $derived(page.status === 404);
    const title = $derived(getErrorTitle(page.status));
    const message = $derived(getErrorMessage(page.status, page.error?.message));
</script>

<svelte:head>
    <title>{isNotFound ? 'Page Not Found' : 'Error'} - Exceptionless</title>
</svelte:head>

<section class="mx-auto flex max-w-xl flex-col items-center gap-4 py-16 text-center" aria-labelledby="app-error-title">
    {#if isNotFound}
        <SearchXIcon class="text-muted-foreground size-10" aria-hidden="true" />
    {:else}
        <ShieldAlertIcon class="text-muted-foreground size-10" aria-hidden="true" />
    {/if}
    <h1 id="app-error-title" class="text-2xl font-semibold tracking-tight">{title}</h1>
    <p class="text-muted-foreground">{message}</p>
    <div class="flex flex-wrap justify-center gap-2">
        <Button href={resolve('/(app)/event')}>Open Events</Button>
        <Button href={resolve('/(app)/project/list')} variant="outline">View Projects</Button>
    </div>
</section>
