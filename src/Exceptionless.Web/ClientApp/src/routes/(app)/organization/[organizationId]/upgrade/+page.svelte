<script lang="ts">
    import { goto } from '$app/navigation';
    import { resolve } from '$app/paths';
    import { page } from '$app/state';
    import { onMount } from 'svelte';
    import { SvelteURL } from 'svelte/reactivity';

    onMount(() => {
        const path = resolve('/(app)/organization/[organizationId]/billing', {
            organizationId: page.params.organizationId!
        });
        const destination = new SvelteURL(path, page.url.href);
        destination.search = page.url.search;
        destination.hash = page.url.hash;
        destination.searchParams.set('changePlan', 'true');
        void goto(destination, {
            replace: true
        });
    });
</script>

<p>Opening billing…</p>
