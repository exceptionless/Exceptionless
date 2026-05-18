<script lang="ts">
    import { H3, Muted } from '$comp/typography';
    import { Separator } from '$comp/ui/separator';
    import { accessToken } from '$features/auth/index.svelte';
    import { useHideOrganizationNotifications } from '$features/organizations/hooks/use-hide-organization-notifications.svelte';
    import { getMeQuery } from '$features/users/api.svelte';

    import type { NavigationItemContext } from '../../routes.svelte';

    import SidebarNav from '../(components)/sidebar-nav.svelte';
    import { routes } from './routes.svelte';

    let { children } = $props();

    const meQuery = getMeQuery();
    let isAuthenticated = $derived(accessToken.current !== null);
    const filteredRoutes = $derived.by(() => {
        const context: NavigationItemContext = { authenticated: isAuthenticated, user: meQuery.data };
        return routes().filter((route) => (route.show ? route.show(context) : true));
    });

    useHideOrganizationNotifications();
</script>

<H3>Settings</H3>
<Muted>Manage your account settings and set e-mail preferences.</Muted>

<Separator class="m-6 w-auto" />

<SidebarNav class="overflow-x-auto pb-2 lg:flex-row lg:space-y-0 lg:space-x-2" routes={filteredRoutes} />

<Separator class="m-6 w-auto" />

{@render children()}
