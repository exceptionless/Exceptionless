<script lang="ts">
    import type { ComponentProps, Snippet } from 'svelte';

    import { resolve } from '$app/paths';
    import { page } from '$app/state';
    import { A } from '$comp/typography';
    import * as Collapsible from '$comp/ui/collapsible';
    import * as Sidebar from '$comp/ui/sidebar';
    import { useSidebar } from '$comp/ui/sidebar';
    import ChevronRight from '@lucide/svelte/icons/chevron-right';
    import Settings from '@lucide/svelte/icons/settings-2';
    import Wrench from '@lucide/svelte/icons/wrench';

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

    const systemRoutes = $derived(routes.filter((route) => route.group === 'System'));
    const systemBasePath = resolve('/(app)/system');
    const systemIsActive = $derived(page.url.pathname === systemBasePath || page.url.pathname.startsWith(systemBasePath + '/'));

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
        <Sidebar.Group class="pt-1">
            <Sidebar.Menu>
                {#each dashboardRoutes as route (route.href)}
                    {@const Icon = route.icon}
                    {#if route.children?.length}
                        <Collapsible.Root
                            open={route.href === page.url.pathname || route.children?.some((c) => page.url.href.includes(c.href))}
                            class="group/collapsible"
                        >
                            {#snippet child({ props: collapsibleProps })}
                                <Sidebar.MenuItem {...collapsibleProps}>
                                    <Collapsible.Trigger>
                                        {#snippet child({ props: triggerProps })}
                                            <Sidebar.MenuButton isActive={route.href === page.url.pathname} {...triggerProps}>
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
                                            </Sidebar.MenuButton>
                                        {/snippet}
                                    </Collapsible.Trigger>
                                    <Collapsible.Content>
                                        <Sidebar.MenuSub>
                                            {#each route.children as savedItem (savedItem.href)}
                                                {@const savedId = new URL(savedItem.href, page.url.origin).searchParams.get('saved')}
                                                {@const activeSavedParam = page.url.searchParams.get('saved')}
                                                {@const isOnRoute = route.href === page.url.pathname}
                                                {@const isActive = isOnRoute && (savedItem.isDefault ? !activeSavedParam || activeSavedParam === savedId : activeSavedParam === savedId)}
                                                <Sidebar.MenuSubItem>
                                                    <Sidebar.MenuSubButton {isActive}>
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
                            <Sidebar.MenuButton isActive={route.href === page.url.pathname}>
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
            </Sidebar.Menu>
        </Sidebar.Group>

        {#if systemRoutes.length > 0}
            <Sidebar.Group>
                <Sidebar.Menu>
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
