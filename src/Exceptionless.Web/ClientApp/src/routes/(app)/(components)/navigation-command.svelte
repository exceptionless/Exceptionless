<script lang="ts">
    import * as Command from '$comp/ui/command';
    import { appKeyboardShortcuts, formatKeyboardShortcut, type ShortcutKey } from '$features/shared/keyboard-shortcuts';
    import Building2 from '@lucide/svelte/icons/building-2';
    import CircleUserRound from '@lucide/svelte/icons/circle-user-round';
    import Keyboard from '@lucide/svelte/icons/keyboard';

    import type { NavigationItem } from '../../routes.svelte';

    type CommandNavigationItem = {
        group: string;
        href: string;
        icon: NavigationItem['icon'];
        openInNewTab?: boolean;
        parentTitle?: string;
        shortcut?: readonly ShortcutKey[];
        title: string;
        value: string;
    };

    type Props = {
        open: boolean;
        openKeyboardShortcuts: () => Promise<void> | void;
        openOrganizationSwitcher: () => Promise<void> | void;
        openUserMenu: () => Promise<void> | void;
        resetKey: number;
        routes: NavigationItem[];
    };

    let { open = $bindable(), openKeyboardShortcuts, openOrganizationSwitcher, openUserMenu, resetKey, routes }: Props = $props();

    function getCommandGroup(route: NavigationItem): string {
        return route.group === 'Dashboards' ? route.title : route.group;
    }

    function getCommandTitle(route: NavigationItem): string {
        return route.group === 'Dashboards' ? `All ${route.title}` : route.title;
    }

    function getCommandValue(...parts: Array<string | undefined>): string {
        return parts.filter(Boolean).join(' ');
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
                    shortcut: route.shortcut,
                    title,
                    value: getCommandValue(group, route.title, title)
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
                        value: getCommandValue(group, route.title, child.title)
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

    async function switchOrganization(): Promise<void> {
        closeCommandWindow();
        await openOrganizationSwitcher();
    }

    async function openCurrentUserMenu(): Promise<void> {
        closeCommandWindow();
        await openUserMenu();
    }

    async function openKeyboardShortcutsDialog(): Promise<void> {
        closeCommandWindow();
        await openKeyboardShortcuts();
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
                            {#if route.shortcut}
                                <Command.Shortcut>{formatKeyboardShortcut(route.shortcut)}</Command.Shortcut>
                            {/if}
                        </Command.LinkItem>
                    {/each}
                </Command.Group>
                {#if group === 'Sessions'}
                    <Command.Separator />
                    <Command.Group heading="Organizations">
                        <Command.Item value="Switch Organization organizations org" onSelect={() => void switchOrganization()}>
                            <Building2 />
                            <span>Switch Organization</span>
                            <Command.Shortcut>{formatKeyboardShortcut(appKeyboardShortcuts.switchOrganization.keys)}</Command.Shortcut>
                        </Command.Item>
                    </Command.Group>
                    <Command.Separator />
                    <Command.Group heading="User">
                        <Command.Item value="Open User Menu account profile current user" onSelect={() => void openCurrentUserMenu()}>
                            <CircleUserRound />
                            <span>Open User Menu</span>
                            <Command.Shortcut>{formatKeyboardShortcut(appKeyboardShortcuts.userMenu.keys)}</Command.Shortcut>
                        </Command.Item>
                        <Command.Item value="Keyboard Shortcuts help shortcuts" onSelect={() => void openKeyboardShortcutsDialog()}>
                            <Keyboard />
                            <span>Keyboard Shortcuts</span>
                            <Command.Shortcut>{formatKeyboardShortcut(appKeyboardShortcuts.keyboardShortcuts.keys)}</Command.Shortcut>
                        </Command.Item>
                    </Command.Group>
                {/if}
                {#if index !== Object.keys(groupedRoutes).length - 1}
                    <Command.Separator />
                {/if}
            {/each}
        </Command.List>
    </Command.Dialog>
{/key}
