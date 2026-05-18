<script lang="ts">
    import type { ComponentProps, Snippet } from 'svelte';
    import { onDestroy } from 'svelte';

    import { resolve } from '$app/paths';
    import { page } from '$app/state';
    import { A } from '$comp/typography';
    import * as Collapsible from '$comp/ui/collapsible';
    import * as DropdownMenu from '$comp/ui/dropdown-menu';
    import * as Sidebar from '$comp/ui/sidebar';
    import { useSidebar } from '$comp/ui/sidebar';
    import { getProjectQuery } from '$features/projects/api.svelte';
    import ChevronRight from '@lucide/svelte/icons/chevron-right';
    import LayoutDashboard from '@lucide/svelte/icons/layout-dashboard';
    import Settings from '@lucide/svelte/icons/settings-2';
    import User from '@lucide/svelte/icons/user';
    import Wrench from '@lucide/svelte/icons/wrench';

    import type { NavigationItem } from '../../../routes.svelte';

    function isSavedItemActive(savedItem: { href: string; isDefault?: boolean }, routeHref: string): boolean {
        const savedId = new URL(savedItem.href, page.url.origin).searchParams.get('saved');
        const activeSavedParam = page.url.searchParams.get('saved');
        const isOnRoute = routeHref === page.url.pathname;

        return isOnRoute && (savedItem.isDefault ? !activeSavedParam || activeSavedParam === savedId : activeSavedParam === savedId);
    }

    function isRouteActive(href: string): boolean {
        return page.url.pathname === href || page.url.pathname.startsWith(`${href}/`);
    }

    function isChildItemActive(childItem: { href: string; isDefault?: boolean }, routeHref: string): boolean {
        const childUrl = new URL(childItem.href, page.url.origin);
        const hasSavedViewParam = childUrl.searchParams.has('saved');

        if (hasSavedViewParam || childItem.isDefault !== undefined) {
            return isSavedItemActive(childItem, routeHref);
        }

        return isRouteActive(childUrl.pathname);
    }

    type Props = ComponentProps<typeof Sidebar.Root> & {
        footer?: Snippet;
        header?: Snippet;
        routes: NavigationItem[];
    };

    let { footer, header, routes, ...props }: Props = $props();
    const dashboardRoutes = $derived(routes.filter((route) => route.group === 'Dashboards'));
    const dashboardsIsActive = $derived(dashboardRoutes.some((route) => isRouteActive(String(route.href))));

    const settingsRoutes = $derived(routes.filter((route) => route.group === 'Settings'));
    const projectSettingsRoutes = $derived(routes.filter((route) => route.group === 'Project Settings'));
    const settingsIsActive = $derived(settingsRoutes.some((route) => isRouteActive(String(route.href))) || projectSettingsRoutes.some((route) => isRouteActive(String(route.href))));
    const currentProjectId = $derived(page.params.projectId);
    const currentProjectQuery = getProjectQuery({
        route: {
            get id() {
                return currentProjectId;
            }
        }
    });
    const currentProjectName = $derived(currentProjectQuery.data?.name ?? currentProjectId ?? 'Project');

    const systemRoutes = $derived(routes.filter((route) => route.group === 'System'));
    const systemBasePath = resolve('/(app)/system');
    const systemIsActive = $derived(page.url.pathname === systemBasePath || page.url.pathname.startsWith(systemBasePath + '/'));
    const accountRoutes = $derived(routes.filter((route) => route.group === 'My Account'));
    const accountIsActive = $derived(accountRoutes.some((route) => isRouteActive(String(route.href))));

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
                {#if isIconCollapsed}
                    {@const menuId = 'section:dashboards'}
                    <DropdownMenu.Root open={isHoverMenuOpen(menuId)} onOpenChange={(open) => onHoverMenuOpenChange(menuId, open)}>
                        <DropdownMenu.Trigger>
                            {#snippet child({ props })}
                                <Sidebar.MenuItem onmouseenter={() => openHoverMenu(menuId)} onmouseleave={() => closeHoverMenu(menuId)}>
                                    <Sidebar.MenuButton {...props}>
                                        <LayoutDashboard />
                                        <span>Dashboards</span>
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
                            {#each dashboardRoutes as route (route.href)}
                                {#if route.children?.length}
                                    <DropdownMenu.Sub>
                                        <DropdownMenu.SubTrigger
                                            onmouseenter={() => openHoverMenu(menuId)}
                                            onmouseleave={() => closeHoverMenu(menuId)}
                                        >
                                            {route.title}
                                        </DropdownMenu.SubTrigger>
                                        <DropdownMenu.SubContent
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
                                        </DropdownMenu.SubContent>
                                    </DropdownMenu.Sub>
                                {:else}
                                    <DropdownMenu.Item>
                                        <A variant="ghost" href={route.href} class="w-full" onclick={onFlyoutLinkClick}>
                                            {route.title}
                                        </A>
                                    </DropdownMenu.Item>
                                {/if}
                            {/each}
                        </DropdownMenu.Content>
                    </DropdownMenu.Root>
                {:else}
                    <Collapsible.Root open={dashboardsIsActive} class="group/collapsible">
                        {#snippet child({ props })}
                            <Sidebar.MenuItem {...props}>
                                <Collapsible.Trigger>
                                    {#snippet child({ props })}
                                        <Sidebar.MenuButton {...props}>
                                            <LayoutDashboard />
                                            <span>Dashboards</span>
                                            <ChevronRight class="ml-auto transition-transform duration-200 group-data-[state=open]/collapsible:rotate-90" />
                                        </Sidebar.MenuButton>
                                    {/snippet}
                                </Collapsible.Trigger>
                                <Collapsible.Content>
                                    <Sidebar.MenuSub>
                                        {#each dashboardRoutes as route (route.href)}
                                            {@const Icon = route.icon}
                                            {#if route.children?.length}
                                                {@const isChildActive = route.href === page.url.pathname || route.children.some((childItem) => isChildItemActive(childItem, route.href))}
                                                <Collapsible.Root open={isChildActive} class="group/collapsible">
                                                    {#snippet child({ props: collapsibleProps })}
                                                        <Sidebar.MenuSubItem {...collapsibleProps}>
                                                            <Collapsible.Trigger>
                                                                {#snippet child({ props: triggerProps })}
                                                                    <Sidebar.MenuSubButton isActive={route.href === page.url.pathname} {...triggerProps}>
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
                                                                                <ChevronRight
                                                                                    class="ml-auto transition-transform duration-200 group-data-[state=open]/collapsible:rotate-90"
                                                                                />
                                                                            </A>
                                                                        {/snippet}
                                                                    </Sidebar.MenuSubButton>
                                                                {/snippet}
                                                            </Collapsible.Trigger>
                                                            <Collapsible.Content>
                                                                <Sidebar.MenuSub class="ml-4">
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
                                                        </Sidebar.MenuSubItem>
                                                    {/snippet}
                                                </Collapsible.Root>
                                            {:else}
                                                <Sidebar.MenuSubItem>
                                                    <Sidebar.MenuSubButton isActive={route.href === page.url.pathname}>
                                                        {#snippet child({ props })}
                                                            <A variant="ghost" href={route.href} title={route.title} onclick={onMenuClick} {...props}>
                                                                <Icon />
                                                                <span>{route.title}</span>
                                                            </A>
                                                        {/snippet}
                                                    </Sidebar.MenuSubButton>
                                                </Sidebar.MenuSubItem>
                                            {/if}
                                        {/each}
                                    </Sidebar.MenuSub>
                                </Collapsible.Content>
                            </Sidebar.MenuItem>
                        {/snippet}
                    </Collapsible.Root>
                {/if}
            </Sidebar.Menu>
        </Sidebar.Group>

        <Sidebar.Group>
            <Sidebar.Menu>
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
                                {#if subItem.title === 'Projects' && projectSettingsRoutes.length > 0}
                                    <DropdownMenu.Separator />
                                    <DropdownMenu.Label>{currentProjectName}</DropdownMenu.Label>
                                    {#each projectSettingsRoutes as projectSubItem (projectSubItem.href)}
                                        <DropdownMenu.Item>
                                            <A variant="ghost" href={projectSubItem.href} class="w-full" onclick={onFlyoutLinkClick}>
                                                {projectSubItem.title}
                                            </A>
                                        </DropdownMenu.Item>
                                    {/each}
                                {/if}
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
                                        {#each settingsRoutes as subItem, index (subItem.href)}
                                            {#if index > 0 && settingsRoutes[index - 1]?.title === 'Organizations'}
                                                <Sidebar.MenuSubItem>
                                                    <div class="border-border mx-2 my-1 w-auto border-t"></div>
                                                </Sidebar.MenuSubItem>
                                            {/if}
                                            <Sidebar.MenuSubItem>
                                                <Sidebar.MenuSubButton isActive={isRouteActive(String(subItem.href))}>
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
                                            {#if subItem.title === 'Projects' && projectSettingsRoutes.length > 0}
                                                <Sidebar.MenuSubItem class="relative ml-4 before:bg-border before:absolute before:top-0 before:bottom-0 before:left-0 before:w-px">
                                                    <div
                                                        class="text-muted-foreground truncate px-2 py-1 pl-6 text-xs font-medium"
                                                        title={currentProjectName}
                                                    >
                                                        {currentProjectName}
                                                    </div>
                                                </Sidebar.MenuSubItem>
                                                {#each projectSettingsRoutes as projectSubItem (projectSubItem.href)}
                                                    <Sidebar.MenuSubItem class="relative ml-4 before:bg-border before:absolute before:top-0 before:bottom-0 before:left-0 before:w-px">
                                                        <Sidebar.MenuSubButton class="pl-6" isActive={isRouteActive(String(projectSubItem.href))}>
                                                            {#snippet child({ props })}
                                                                <A
                                                                    variant="ghost"
                                                                    href={projectSubItem.href}
                                                                    title={projectSubItem.title}
                                                                    onclick={onMenuClick}
                                                                    {...props}
                                                                >
                                                                    {#if projectSubItem.icon}
                                                                        {@const Icon = projectSubItem.icon}
                                                                        <Icon />
                                                                    {/if}
                                                                    <span>{projectSubItem.title}</span>
                                                                </A>
                                                            {/snippet}
                                                        </Sidebar.MenuSubButton>
                                                    </Sidebar.MenuSubItem>
                                                {/each}
                                            {/if}
                                        {/each}
                                    </Sidebar.MenuSub>
                                </Collapsible.Content>
                            </Sidebar.MenuItem>
                        {/snippet}
                    </Collapsible.Root>
                {/if}
            </Sidebar.Menu>
        </Sidebar.Group>

        {#if accountRoutes.length > 0}
            <Sidebar.Group>
                <Sidebar.Menu>
                    {#if isIconCollapsed}
                        {@const menuId = 'section:account'}
                        <DropdownMenu.Root open={isHoverMenuOpen(menuId)} onOpenChange={(open) => onHoverMenuOpenChange(menuId, open)}>
                            <DropdownMenu.Trigger>
                                {#snippet child({ props })}
                                    <Sidebar.MenuItem onmouseenter={() => openHoverMenu(menuId)} onmouseleave={() => closeHoverMenu(menuId)}>
                                        <Sidebar.MenuButton {...props}>
                                            <User />
                                            <span>Account</span>
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
                                {#each accountRoutes as subItem (subItem.href)}
                                    <DropdownMenu.Item>
                                        <A variant="ghost" href={subItem.href} class="w-full" onclick={onFlyoutLinkClick}>
                                            {subItem.title}
                                        </A>
                                    </DropdownMenu.Item>
                                {/each}
                            </DropdownMenu.Content>
                        </DropdownMenu.Root>
                    {:else}
                        <Collapsible.Root open={accountIsActive} class="group/collapsible">
                            {#snippet child({ props })}
                                <Sidebar.MenuItem {...props}>
                                    <Collapsible.Trigger>
                                        {#snippet child({ props })}
                                            <Sidebar.MenuButton {...props}>
                                                <User />
                                                <span>Account</span>
                                                <ChevronRight class="ml-auto transition-transform duration-200 group-data-[state=open]/collapsible:rotate-90" />
                                            </Sidebar.MenuButton>
                                        {/snippet}
                                    </Collapsible.Trigger>
                                    <Collapsible.Content>
                                        <Sidebar.MenuSub>
                                            {#each accountRoutes as subItem (subItem.href)}
                                                <Sidebar.MenuSubItem>
                                                    <Sidebar.MenuSubButton isActive={isRouteActive(String(subItem.href))}>
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
                                        </Sidebar.MenuSub>
                                    </Collapsible.Content>
                                </Sidebar.MenuItem>
                            {/snippet}
                        </Collapsible.Root>
                    {/if}
                </Sidebar.Menu>
            </Sidebar.Group>
        {/if}

        {#if systemRoutes.length > 0}
            <Sidebar.Group>
                <Sidebar.Menu>
                    {#if isIconCollapsed}
                        {@const menuId = 'section:system'}
                        <DropdownMenu.Root open={isHoverMenuOpen(menuId)} onOpenChange={(open) => onHoverMenuOpenChange(menuId, open)}>
                            <DropdownMenu.Trigger>
                                {#snippet child({ props })}
                                    <Sidebar.MenuItem onmouseenter={() => openHoverMenu(menuId)} onmouseleave={() => closeHoverMenu(menuId)}>
                                        <Sidebar.MenuButton {...props}>
                                            <Wrench />
                                            <span>System</span>
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
                                {#each systemRoutes as subItem (subItem.href)}
                                    <DropdownMenu.Item>
                                        <A variant="ghost" href={subItem.href} class="w-full" onclick={onFlyoutLinkClick}>
                                            {subItem.title}
                                        </A>
                                    </DropdownMenu.Item>
                                {/each}
                            </DropdownMenu.Content>
                        </DropdownMenu.Root>
                    {:else}
                        <Collapsible.Root open={systemIsActive} class="group/collapsible">
                            {#snippet child({ props })}
                                <Sidebar.MenuItem {...props}>
                                    <Collapsible.Trigger>
                                        {#snippet child({ props })}
                                            <Sidebar.MenuButton {...props}>
                                                <Wrench />
                                                <span>System</span>
                                                <ChevronRight class="ml-auto transition-transform duration-200 group-data-[state=open]/collapsible:rotate-90" />
                                            </Sidebar.MenuButton>
                                        {/snippet}
                                    </Collapsible.Trigger>
                                    <Collapsible.Content>
                                        <Sidebar.MenuSub>
                                            {#each systemRoutes as subItem (subItem.href)}
                                                <Sidebar.MenuSubItem>
                                                    <Sidebar.MenuSubButton isActive={subItem.href === page.url.pathname}>
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
                                        </Sidebar.MenuSub>
                                    </Collapsible.Content>
                                </Sidebar.MenuItem>
                            {/snippet}
                        </Collapsible.Root>
                    {/if}
                </Sidebar.Menu>
            </Sidebar.Group>
        {/if}
    </Sidebar.Content>
    <Sidebar.Rail />
    <Sidebar.Footer>
        {#if footer}
            {@render footer()}
        {/if}
    </Sidebar.Footer>
</Sidebar.Root>
