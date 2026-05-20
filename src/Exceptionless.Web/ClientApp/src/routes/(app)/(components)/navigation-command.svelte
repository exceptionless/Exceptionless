<script lang="ts">
    import * as Command from '$comp/ui/command';

    import type { NavigationItem } from '../../routes.svelte';

    type CommandNavigationItem = {
        group: string;
        href: string;
        icon: NavigationItem['icon'];
        openInNewTab?: boolean;
        parentTitle?: string;
        title: string;
        value: string;
    };

    let { open = $bindable(), resetKey, routes }: { open: boolean; resetKey: number; routes: NavigationItem[] } = $props();

    function getCommandGroup(route: NavigationItem): string {
        return route.group === 'Dashboards' ? route.title : route.group;
    }

    function getCommandTitle(route: NavigationItem): string {
        return route.group === 'Dashboards' ? `All ${route.title}` : route.title;
    }

    const commandRoutes = $derived(
        routes.flatMap((route) => {
            const group = getCommandGroup(route);
            const title = getCommandTitle(route);
            const items: CommandNavigationItem[] = [
                {
                    group,
                    href: route.href,
                    icon: route.icon,
                    openInNewTab: route.openInNewTab,
                    title,
                    value: `${route.title} ${title} ${route.href}`
                }
            ];

            if (route.children?.length) {
                items.push(
                    ...route.children.map((child) => ({
                        group,
                        href: child.href,
                        icon: route.icon,
                        parentTitle: route.group === 'Dashboards' ? undefined : route.title,
                        title: child.title,
                        value: `${route.title} ${child.title} ${child.href}`
                    }))
                );
            }

            return items;
        })
    );

    const groupedRoutes = $derived(
        Object.entries(Object.groupBy(commandRoutes, (item: CommandNavigationItem) => item.group)).reduce(
            (acc, [key, value]) => {
                if (value) {
                    acc[key] = value;
                }

                return acc;
            },
            {} as Record<string, CommandNavigationItem[]>
        )
    );

    function closeCommandWindow() {
        open = false;
    }
</script>

{#key resetKey}
    <Command.Dialog bind:open>
        <Command.Input placeholder="Search or jump to..." />
        <Command.List>
            <Command.Empty>No results found.</Command.Empty>
            {#each Object.entries(groupedRoutes) as [group, items], index (group)}
                <Command.Group heading={group}>
                    {#each items as route (route.href)}
                        <Command.LinkItem
                            href={route.href}
                            onclick={closeCommandWindow}
                            rel={route.openInNewTab ? 'noreferrer' : undefined}
                            target={route.openInNewTab ? '_blank' : undefined}
                            value={route.value}
                        >
                            {#if route.icon}
                                {@const Icon = route.icon}
                                <Icon />
                            {/if}
                            <div class="flex min-w-0 flex-col">
                                <span class="truncate">{route.title}</span>
                                {#if route.parentTitle}
                                    <span class="text-muted-foreground text-xs">{route.parentTitle}</span>
                                {/if}
                            </div>
                        </Command.LinkItem>
                    {/each}
                </Command.Group>
                {#if index !== Object.keys(groupedRoutes).length - 1}
                    <Command.Separator />
                {/if}
            {/each}
        </Command.List>
    </Command.Dialog>
{/key}
