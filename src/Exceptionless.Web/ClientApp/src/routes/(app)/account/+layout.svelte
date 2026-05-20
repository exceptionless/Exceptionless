<script lang="ts">
    import { page } from '$app/state';
    import { A, H3 } from '$comp/typography';
    import { accessToken } from '$features/auth/index.svelte';
    import { useHideOrganizationNotifications } from '$features/organizations/hooks/use-hide-organization-notifications.svelte';
    import { getMeQuery } from '$features/users/api.svelte';

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

    useHideOrganizationNotifications();
</script>

<div>
    <H3>Account</H3>
    <div class="mt-6 space-y-6">
        <nav class="bg-muted flex w-full flex-row flex-nowrap gap-1 overflow-x-auto rounded-lg p-1">
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
</div>
