<script lang="ts">
    import { page } from '$app/state';
    import { A, H3, Muted } from '$comp/typography';
    import { accessToken } from '$features/auth/index.svelte';
    import { getMeQuery } from '$features/users/api.svelte';
    import GlobalUser from '$features/users/components/global-user.svelte';

    import type { NavigationItemContext } from '../../routes.svelte';

    import { routes } from './routes.svelte';

    let { children } = $props();

    const meQuery = getMeQuery();
    let isAuthenticated = $derived(accessToken.current !== null);
    const filteredRoutes = $derived.by(() => {
        const context: NavigationItemContext = { authenticated: isAuthenticated, user: meQuery.data };
        return routes().filter((route) => (route.show ? route.show(context) : true));
    });
    const currentPath = $derived(page.url.pathname);
</script>

<GlobalUser>
    <H3>System Administration</H3>
    <Muted>Manage Exceptionless system maintenance and operations</Muted>
    <div class="mt-6 space-y-6">
        <nav class="flex w-full flex-row flex-nowrap gap-1 overflow-x-auto rounded-lg bg-muted p-1">
            {#each filteredRoutes as route (route.href)}
                {@const isActive = currentPath === route.href || currentPath.startsWith(route.href + '/')}
                <A
                    variant="ghost"
                    href={route.href}
                    data-sveltekit-noscroll
                    class="shrink-0 rounded-md px-3 py-1.5 text-sm font-medium transition-colors {isActive
                        ? 'bg-background text-foreground shadow-xs'
                        : 'text-muted-foreground hover:bg-muted hover:text-foreground'}"
                >
                    {route.title}
                </A>
            {/each}
        </nav>
        {@render children()}
    </div>
    {#snippet disabled()}
        <div class="flex flex-col items-center justify-center gap-2 py-12 text-center text-muted-foreground">
            <span class="text-lg font-medium">Access Denied</span>
            <span class="text-sm">You must be a global administrator to access this page.</span>
        </div>
    {/snippet}
</GlobalUser>
