<script lang="ts">
    import * as Card from '$comp/ui/card';
    import { Separator } from '$comp/ui/separator';
    import { accessToken } from '$features/auth/index.svelte';
    import { getMeQuery } from '$features/users/api.svelte';

    import type { NavigationItemContext } from '../../routes';

    import SidebarNav from './(components)/sidebar-nav.svelte';
    import { routes } from './routes';

    let { children } = $props();

    const userResponse = getMeQuery();
    let isAuthenticated = $derived(accessToken.current !== null);
    const filteredRoutes = $derived.by(() => {
        const context: NavigationItemContext = { authenticated: isAuthenticated, user: userResponse.data };
        return routes.filter((route) => (route.show ? route.show(context) : true));
    });
</script>

<Card.Root
    ><Card.Header>
        <Card.Title class="text-2xl" level={2}>Settings</Card.Title>
        <Card.Description>Manage your account settings and set e-mail preferences.</Card.Description>
    </Card.Header>
    <Separator class="mx-6 my-6 w-auto" />

    <Card.Content>
        <div class="flex flex-col space-y-8 lg:flex-row lg:space-y-0 lg:space-x-12">
            <aside class="-mx-4 lg:w-1/5">
                <SidebarNav routes={filteredRoutes} />
            </aside>
            <div class="flex-1">
                {@render children()}
            </div>
        </div>
    </Card.Content>
</Card.Root>
