<script lang="ts">
    import * as Sidebar from '$comp/ui/sidebar';
    import { useSidebar } from '$comp/ui/sidebar';

    import type { NavigationItem } from '../../../routes';

    interface Props {
        routes: NavigationItem[];
    }

    let { routes }: Props = $props();
    const dashboardRoutes = routes.filter((route) => route.group === 'Dashboards');

    const sidebar = useSidebar();
    // const collapsible = $derived(!isLargeScreen ? 'offcanvas' : 'icon');
    // const variant = $derived(isLargeScreen ? 'sidebar' : 'floating');
</script>

<Sidebar.Root collapsible="icon">
    <Sidebar.Content class={!sidebar.openMobile ? 'mt-16' : ''}>
        <Sidebar.Group>
            <Sidebar.Menu>
                {#each dashboardRoutes as route (route.href)}
                    {@const Icon = route.icon}
                    <Sidebar.MenuItem>
                        <Sidebar.MenuButton isActive={true}>
                            {#snippet child({ props })}
                                <a href={route.href} title={route.title} {...props}>
                                    <Icon />
                                    <span>{route.title}</span>
                                </a>
                            {/snippet}
                        </Sidebar.MenuButton>
                    </Sidebar.MenuItem>
                {/each}
            </Sidebar.Menu>
        </Sidebar.Group>
    </Sidebar.Content>
</Sidebar.Root>
