<script lang="ts">
    import type { ComponentProps, Snippet } from 'svelte';

    import { page } from '$app/state';
    import * as Sidebar from '$comp/ui/sidebar';
    import { useSidebar } from '$comp/ui/sidebar';

    import type { NavigationItem } from '../../../routes';

    type Props = ComponentProps<typeof Sidebar.Root> & {
        footer?: Snippet;
        header?: Snippet;
        routes: NavigationItem[];
    };

    let { footer, header, routes, ...props }: Props = $props();
    const dashboardRoutes = routes.filter((route) => route.group === 'Dashboards');

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
    </Sidebar.Content>
    <Sidebar.Rail />
    <Sidebar.Footer>
        {#if footer}
            {@render footer()}
        {/if}
    </Sidebar.Footer>
</Sidebar.Root>
