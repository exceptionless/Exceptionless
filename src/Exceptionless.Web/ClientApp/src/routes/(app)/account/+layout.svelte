<script lang="ts">
    import { H3, Muted } from '$comp/typography';
    import { Separator } from '$comp/ui/separator';
    import { accessToken } from '$features/auth/index.svelte';
    import * as SplitLayout from '$features/shared/components/layouts/split-layout';
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
</script>

<H3>Settings</H3>
<Muted>Manage your account settings and set e-mail preferences.</Muted>

<Separator class="mx-6 my-6 w-auto" />

<SplitLayout.Root>
    <SplitLayout.Sidebar>
        <SidebarNav routes={filteredRoutes} />
    </SplitLayout.Sidebar>
    <SplitLayout.Content>
        {@render children()}
    </SplitLayout.Content>
</SplitLayout.Root>
