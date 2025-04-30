<script lang="ts">
    import type { ComponentProps, Snippet } from 'svelte';

    import { page } from '$app/state';
    import * as Collapsible from '$comp/ui/collapsible';
    import * as Sidebar from '$comp/ui/sidebar';
    import { useSidebar } from '$comp/ui/sidebar';
    import ChevronRight from '@lucide/svelte/icons/chevron-right';
    import Settings from '@lucide/svelte/icons/settings-2';

    import type { NavigationItem } from '../../../routes.svelte';

    type Props = ComponentProps<typeof Sidebar.Root> & {
        footer?: Snippet;
        header?: Snippet;
        routes: NavigationItem[];
    };

    let { footer, header, routes, ...props }: Props = $props();
    const dashboardRoutes = $derived(routes.filter((route) => route.group === 'Dashboards'));
    const settingsRoutes = $derived(routes.filter((route) => route.group === 'Settings'));
    const settingsIsActive = $derived(settingsRoutes.some((route) => route.href === page.url.pathname));

    const sidebar = useSidebar();

    function onMenuClick() {
        if (sidebar.isMobile) {
            sidebar.toggle();
        }
    }
</script>

<Sidebar.Root collapsible="icon" {...props}>
    <Sidebar.Header class={!sidebar.isMobile ? 'mt-16' : ''}>
        {#if header}
            {@render header()}
        {/if}
    </Sidebar.Header>
    <Sidebar.Content>
        <Sidebar.Group class="pt-0">
            <Sidebar.Menu>
                {#each dashboardRoutes as route (route.href)}
                    {@const Icon = route.icon}
                    <Sidebar.MenuItem>
                        <Sidebar.MenuButton isActive={route.href === page.url.pathname}>
                            {#snippet child({ props })}
                                <a href={route.href} title={route.title} onclick={onMenuClick} {...props}>
                                    <Icon />
                                    <span>{route.title}</span>
                                </a>
                            {/snippet}
                        </Sidebar.MenuButton>
                    </Sidebar.MenuItem>
                {/each}
            </Sidebar.Menu>
        </Sidebar.Group>

        <Sidebar.Group>
            <Sidebar.Menu>
                <Collapsible.Root open={settingsIsActive} class="group/collapsible">
                    {#snippet child({ props })}
                        <Sidebar.MenuItem {...props}>
                            <Collapsible.Trigger>
                                {#snippet child({ props })}
                                    <Sidebar.MenuButton {...props}>
                                        <Settings />
                                        <span>Settings</span>
                                        <ChevronRight class="ml-auto transition-transform duration-200 group-data-[state=open]/collapsible:rotate-90" />
                                    </Sidebar.MenuButton>
                                {/snippet}
                            </Collapsible.Trigger>
                            <Collapsible.Content>
                                <Sidebar.MenuSub>
                                    {#each settingsRoutes as subItem (subItem.href)}
                                        <Sidebar.MenuSubItem>
                                            <Sidebar.MenuSubButton isActive={subItem.href === page.url.pathname}>
                                                {#snippet child({ props })}
                                                    <a href={subItem.href} title={subItem.title} onclick={onMenuClick} {...props}>
                                                        <span>{subItem.title}</span>
                                                    </a>
                                                {/snippet}
                                            </Sidebar.MenuSubButton>
                                        </Sidebar.MenuSubItem>
                                    {/each}
                                </Sidebar.MenuSub>
                            </Collapsible.Content>
                        </Sidebar.MenuItem>
                    {/snippet}
                </Collapsible.Root>
            </Sidebar.Menu>
        </Sidebar.Group>
    </Sidebar.Content>
    <Sidebar.Rail />
    <Sidebar.Footer>
        {#if footer}
            {@render footer()}
        {/if}
    </Sidebar.Footer>
</Sidebar.Root>
