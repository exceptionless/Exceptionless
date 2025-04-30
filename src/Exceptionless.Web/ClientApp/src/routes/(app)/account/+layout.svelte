<script lang="ts">
    import * as Card from '$comp/ui/card';
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

<Card.Root>
    <Card.Header>
        <Card.Title class="text-2xl" level={2}>Settings</Card.Title>
        <Card.Description>Manage your account settings and set e-mail preferences.</Card.Description>
    </Card.Header>
    <Separator class="mx-6 my-6 w-auto" />

    <Card.Content>
        <SplitLayout.Root>
            <SplitLayout.Sidebar>
                <SidebarNav routes={filteredRoutes} />
            </SplitLayout.Sidebar>
            <SplitLayout.Content>
                {@render children()}
            </SplitLayout.Content>
        </SplitLayout.Root>
    </Card.Content>
</Card.Root>
