<script lang="ts">
    import { H3, Muted } from '$comp/typography';
    import { Separator } from '$comp/ui/separator';
    import { accessToken } from '$features/auth/index.svelte';
    import * as SplitLayout from '$features/shared/components/layouts/split-layout';
    import { getMeQuery } from '$features/users/api.svelte';
    import GlobalUser from '$features/users/components/global-user.svelte';

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
</script>

<GlobalUser>
    <H3>System Administration</H3>
    <Muted>Manage Exceptionless system maintenance and operations.</Muted>
    <Separator class="m-6 w-auto" />
    <SplitLayout.Root>
        <SplitLayout.Sidebar>
            <SidebarNav routes={filteredRoutes} />
        </SplitLayout.Sidebar>
        <SplitLayout.Content>
            {@render children()}
        </SplitLayout.Content>
    </SplitLayout.Root>
    {#snippet disabled()}
        <div class="text-muted-foreground flex flex-col items-center justify-center gap-2 py-12 text-center">
            <span class="text-lg font-medium">Access Denied</span>
            <span class="text-sm">You must be a global administrator to access this page.</span>
        </div>
    {/snippet}
</GlobalUser>
