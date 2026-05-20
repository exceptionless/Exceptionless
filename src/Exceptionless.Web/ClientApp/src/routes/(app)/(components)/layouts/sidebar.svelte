<script lang="ts">
    import type { ComponentProps, Snippet } from 'svelte';

    import { resolve } from '$app/paths';
    import { page } from '$app/state';
    import { A } from '$comp/typography';
    import * as Collapsible from '$comp/ui/collapsible';
    import * as DropdownMenu from '$comp/ui/dropdown-menu';
    import * as Sidebar from '$comp/ui/sidebar';
    import { useSidebar } from '$comp/ui/sidebar';
    import ChevronRight from '@lucide/svelte/icons/chevron-right';
    import Settings from '@lucide/svelte/icons/settings-2';
    import Wrench from '@lucide/svelte/icons/wrench';
    import { onDestroy } from 'svelte';

    import type { NavigationItem } from '../../../routes.svelte';

    function isSavedItemActive(savedItem: { href: string; isDefault?: boolean }, routeHref: string): boolean {
        const savedId = new URL(savedItem.href, page.url.origin).searchParams.get('saved');
        const activeSavedParam = page.url.searchParams.get('saved');
        const isOnRoute = routeHref === page.url.pathname;

        return isOnRoute && (savedItem.isDefault ? !activeSavedParam || activeSavedParam === savedId : activeSavedParam === savedId);
    }

    function isPathActive(href: string): boolean {
        return page.url.pathname === href || page.url.pathname.startsWith(href + '/');
    }

    function isSettingsGroup(group: string): boolean {
        return group === 'Settings' || group.endsWith(' Settings');
    }

    function isChildItemActive(childItem: { href: string; isDefault?: boolean }, routeHref: string): boolean {
        const childUrl = new URL(childItem.href, page.url.origin);
        const hasSavedViewParam = childUrl.searchParams.has('saved');

        if (hasSavedViewParam || childItem.isDefault !== undefined) {
            return isSavedItemActive(childItem, routeHref);
        }

        return isPathActive(childUrl.pathname);
    }

    function isRouteActive(route: NavigationItem): boolean {
        const routeHref = String(route.href);
        if (isPathActive(routeHref)) {
            return true;
        }

        return route.children?.some((childItem) => isChildItemActive(childItem, routeHref)) ?? false;
    }

    type Props = ComponentProps<typeof Sidebar.Root> & {
        footer?: Snippet;
        header?: Snippet;
        routes: NavigationItem[];
    };

    let { footer, header, routes, ...props }: Props = $props();
    const dashboardRoutes = $derived(routes.filter((route) => route.group === 'Dashboards'));

    const settingsRoutes = $derived(routes.filter((route) => route.group === 'Settings'));
    const organizationSettingsRoutes = $derived(routes.filter((route) => route.group === 'Organization Settings'));
    const projectSettingsRoutes = $derived(routes.filter((route) => route.group === 'Project Settings'));

    const systemRoutes = $derived(routes.filter((route) => route.group === 'System'));
    const systemRoute = $derived(systemRoutes[0]);
    const systemBasePath = resolve('/(app)/system');
    const systemIsActive = $derived(page.url.pathname === systemBasePath || page.url.pathname.startsWith(systemBasePath + '/'));
    const settingsIsActive = $derived(routes.some((route) => isSettingsGroup(route.group) && isPathActive(route.href)) || systemIsActive);

    function isSettingsRouteActive(route: NavigationItem): boolean {
        if (isPathActive(String(route.href))) {
            return true;
        }

        if (route.title === 'Organizations') {
            return organizationSettingsRoutes.some((organizationSettingsRoute) => isPathActive(String(organizationSettingsRoute.href)));
        }

        return route.title === 'Projects' && projectSettingsRoutes.some((projectSettingsRoute) => isPathActive(String(projectSettingsRoute.href)));
    }

    const sidebar = useSidebar();
    const isIconCollapsed = $derived(sidebar.state === 'collapsed' && !sidebar.isMobile);
    let hoverMenuId = $state<string | undefined>(undefined);
    let hoverMenuCloseTimeout = $state<ReturnType<typeof setTimeout> | undefined>(undefined);

    function onMenuClick() {
        if (sidebar.isMobile) {
            sidebar.toggle();
        }
    }

    function openHoverMenu(menuId: string) {
        if (!isIconCollapsed) {
            return;
        }

        if (hoverMenuCloseTimeout) {
            clearTimeout(hoverMenuCloseTimeout);
            hoverMenuCloseTimeout = undefined;
        }

        hoverMenuId = menuId;
    }

    function closeHoverMenu(menuId: string) {
        if (!isIconCollapsed) {
            return;
        }

        if (hoverMenuCloseTimeout) {
            clearTimeout(hoverMenuCloseTimeout);
        }

        hoverMenuCloseTimeout = setTimeout(() => {
            if (hoverMenuId === menuId) {
                hoverMenuId = undefined;
            }
        }, 220);
    }

    function isHoverMenuOpen(menuId: string): boolean {
        return isIconCollapsed && hoverMenuId === menuId;
    }

    function onHoverMenuOpenChange(menuId: string, open: boolean): void {
        if (!isIconCollapsed) {
            hoverMenuId = undefined;
            return;
        }

        if (open) {
            openHoverMenu(menuId);
            return;
        }

        if (hoverMenuId === menuId) {
            hoverMenuId = undefined;
        }
    }

    function onFlyoutLinkClick(): void {
        hoverMenuId = undefined;
        onMenuClick();
    }

    onDestroy(() => {
        if (hoverMenuCloseTimeout) {
            clearTimeout(hoverMenuCloseTimeout);
        }
    });
</script>

<Sidebar.Root collapsible="icon" {...props}>
    <Sidebar.Header class={!sidebar.isMobile ? 'mt-16' : ''}>
        {#if header}
            {@render header()}
        {/if}
    </Sidebar.Header>
    <Sidebar.Content>
        <Sidebar.Group class="pt-1">
            <Sidebar.Menu>
                {#each dashboardRoutes as route (route.href)}
                    {@const Icon = route.icon}
                    {#if isIconCollapsed}
                        {#if route.children?.length}
                            {@const menuId = `route:${route.href}`}
                            <DropdownMenu.Root open={isHoverMenuOpen(menuId)} onOpenChange={(open) => onHoverMenuOpenChange(menuId, open)}>
                                <DropdownMenu.Trigger>
                                    {#snippet child({ props })}
                                        <Sidebar.MenuItem onmouseenter={() => openHoverMenu(menuId)} onmouseleave={() => closeHoverMenu(menuId)}>
                                            <Sidebar.MenuButton tooltipContent={route.title} {...props}>
                                                <Icon />
                                                <span>{route.title}</span>
                                            </Sidebar.MenuButton>
                                        </Sidebar.MenuItem>
                                    {/snippet}
                                </DropdownMenu.Trigger>
                                <DropdownMenu.Content
                                    side="right"
                                    align="start"
                                    class="w-56"
                                    onmouseenter={() => openHoverMenu(menuId)}
                                    onmouseleave={() => closeHoverMenu(menuId)}
                                >
                                    <DropdownMenu.Item>
                                        <A variant="ghost" href={route.href} class="w-full" onclick={onFlyoutLinkClick}>
                                            {route.title}
                                        </A>
                                    </DropdownMenu.Item>
                                    <DropdownMenu.Separator />
                                    {#each route.children as savedItem (savedItem.href)}
                                        <DropdownMenu.Item>
                                            <A variant="ghost" href={savedItem.href} class="w-full" onclick={onFlyoutLinkClick}>
                                                {savedItem.title}
                                            </A>
                                        </DropdownMenu.Item>
                                    {/each}
                                </DropdownMenu.Content>
                            </DropdownMenu.Root>
                        {:else}
                            <Sidebar.MenuItem>
                                <Sidebar.MenuButton isActive={isRouteActive(route)} tooltipContent={route.title}>
                                    {#snippet child({ props })}
                                        <A variant="ghost" href={route.href} title={route.title} onclick={onMenuClick} {...props}>
                                            <Icon />
                                            <span>{route.title}</span>
                                        </A>
                                    {/snippet}
                                </Sidebar.MenuButton>
                            </Sidebar.MenuItem>
                        {/if}
                    {:else if route.children?.length}
                        <Collapsible.Root open={isRouteActive(route)} class="group/collapsible">
                            {#snippet child({ props: collapsibleProps })}
                                <Sidebar.MenuItem {...collapsibleProps}>
                                    <Collapsible.Trigger>
                                        {#snippet child({ props: triggerProps })}
                                            <Sidebar.MenuButton {...triggerProps}>
                                                {#snippet child({ props: buttonProps })}
                                                    <A
                                                        variant="ghost"
                                                        href={route.href}
                                                        title={route.title}
                                                        onclick={onMenuClick}
                                                        class="flex min-w-0 flex-1 items-center gap-2"
                                                        {...buttonProps}
                                                    >
                                                        <Icon />
                                                        <span>{route.title}</span>
                                                        <ChevronRight class="ml-auto transition-transform duration-200 group-data-[state=open]/collapsible:rotate-90" />
                                                    </A>
                                                {/snippet}
                                            </Sidebar.MenuButton>
                                        {/snippet}
                                    </Collapsible.Trigger>
                                    <Collapsible.Content>
                                        <Sidebar.MenuSub>
                                            {#each route.children as savedItem (savedItem.href)}
                                                <Sidebar.MenuSubItem>
                                                    <Sidebar.MenuSubButton isActive={isChildItemActive(savedItem, route.href)}>
                                                        {#snippet child({ props: subProps })}
                                                            <A
                                                                variant="ghost"
                                                                href={savedItem.href}
                                                                title={savedItem.title}
                                                                onclick={onMenuClick}
                                                                {...subProps}
                                                            >
                                                                <span class="truncate">{savedItem.title}</span>
                                                            </A>
                                                        {/snippet}
                                                    </Sidebar.MenuSubButton>
                                                </Sidebar.MenuSubItem>
                                            {/each}
                                        </Sidebar.MenuSub>
                                    </Collapsible.Content>
                                </Sidebar.MenuItem>
                            {/snippet}
                        </Collapsible.Root>
                    {:else}
                        <Sidebar.MenuItem>
                            <Sidebar.MenuButton isActive={isRouteActive(route)}>
                                {#snippet child({ props })}
                                    <A variant="ghost" href={route.href} title={route.title} onclick={onMenuClick} {...props}>
                                        <Icon />
                                        <span>{route.title}</span>
                                    </A>
                                {/snippet}
                            </Sidebar.MenuButton>
                        </Sidebar.MenuItem>
                    {/if}
                {/each}
                {#if isIconCollapsed}
                    {@const menuId = 'section:settings'}
                    <DropdownMenu.Root open={isHoverMenuOpen(menuId)} onOpenChange={(open) => onHoverMenuOpenChange(menuId, open)}>
                        <DropdownMenu.Trigger>
                            {#snippet child({ props })}
                                <Sidebar.MenuItem onmouseenter={() => openHoverMenu(menuId)} onmouseleave={() => closeHoverMenu(menuId)}>
                                    <Sidebar.MenuButton {...props}>
                                        <Settings />
                                        <span>Settings</span>
                                    </Sidebar.MenuButton>
                                </Sidebar.MenuItem>
                            {/snippet}
                        </DropdownMenu.Trigger>
                        <DropdownMenu.Content
                            side="right"
                            align="start"
                            class="w-56"
                            onmouseenter={() => openHoverMenu(menuId)}
                            onmouseleave={() => closeHoverMenu(menuId)}
                        >
                            {#each settingsRoutes as subItem (subItem.href)}
                                <DropdownMenu.Item>
                                    <A variant="ghost" href={subItem.href} class="w-full" onclick={onFlyoutLinkClick}>
                                        {subItem.title}
                                    </A>
                                </DropdownMenu.Item>
                            {/each}
                        </DropdownMenu.Content>
                    </DropdownMenu.Root>
                {:else}
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
                                                <Sidebar.MenuSubButton isActive={isSettingsRouteActive(subItem)}>
                                                    {#snippet child({ props })}
                                                        <A variant="ghost" href={subItem.href} title={subItem.title} onclick={onMenuClick} {...props}>
                                                            {#if subItem.icon}
                                                                {@const Icon = subItem.icon}
                                                                <Icon />
                                                            {/if}
                                                            <span>{subItem.title}</span>
                                                        </A>
                                                    {/snippet}
                                                </Sidebar.MenuSubButton>
                                            </Sidebar.MenuSubItem>
                                        {/each}
                                        {#if systemRoute}
                                            <Sidebar.MenuSubItem>
                                                <Sidebar.MenuSubButton isActive={systemIsActive}>
                                                    {#snippet child({ props })}
                                                        <A variant="ghost" href={systemRoute.href} title="System" onclick={onMenuClick} {...props}>
                                                            <Wrench />
                                                            <span>System</span>
                                                        </A>
                                                    {/snippet}
                                                </Sidebar.MenuSubButton>
                                            </Sidebar.MenuSubItem>
                                        {/if}
                                    </Sidebar.MenuSub>
                                </Collapsible.Content>
                            </Sidebar.MenuItem>
                        {/snippet}
                    </Collapsible.Root>
                {/if}
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
