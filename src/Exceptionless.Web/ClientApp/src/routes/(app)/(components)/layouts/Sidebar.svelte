<script lang="ts">
    import * as Sidebar from '$comp/ui/sidebar';
    import { useSidebar } from '$comp/ui/sidebar';

    import type { NavigationItem } from '../../../routes';

    interface Props {
        routes: NavigationItem[];
    }

    let { routes }: Props = $props();
    const dashboardRoutes = routes.filter((route) => route.group === 'Dashboards');

    // function onBackdropClick() {
    //     isSidebarOpen = false;
    // }

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

<!-- <aside
    aria-label="Sidebar"
    class="transition-width fixed left-0 top-0 z-20 flex h-full w-64 flex-shrink-0 flex-col bg-background pt-16 text-foreground duration-75 lg:flex {isSidebarOpen
        ? 'lg:w-64'
        : 'hidden lg:w-16'}"
    id="sidebar"
>
    <div class="relative flex min-h-0 flex-1 flex-col border-r pt-0" role="none">
        <div class="flex flex-1 flex-col overflow-y-auto pb-4 pt-5">
            <div class="flex-1 space-y-1 divide-y px-3">
                <ul class="space-y-2 pb-2">
                    {#each dashboardRoutes as route (route.href)}
                        <li>
                            <SidebarMenuItem href={route.href} icon={route.icon} {isLargeScreen} {isSidebarOpen} title={route.title}></SidebarMenuItem>
                        </li>
                    {/each}
                </ul>
            </div>
        </div>
    </div>
</aside>

<button
    aria-label="Close sidebar"
    class="fixed inset-0 z-10 bg-gray-900/50 dark:bg-gray-900/90 {!isLargeScreen && !!isSidebarOpen ? '' : 'hidden'}"
    onclick={onBackdropClick}
    title="Close sidebar"
></button> -->
