<script lang="ts">
    import { A } from '$comp/typography';
    import * as Command from '$comp/ui/command';

    import type { NavigationItem } from '../../routes';

    let { open = $bindable(), routes }: { open: boolean; routes: NavigationItem[] } = $props();

    const groupedRoutes: Record<string, NavigationItem[]> = Object.entries(Object.groupBy(routes, (item: NavigationItem) => item.group)).reduce(
        (acc, [key, value]) => {
            if (value) acc[key] = value;
            return acc;
        },
        {} as Record<string, NavigationItem[]>
    );

    function closeCommandWindow() {
        open = false;
    }
</script>

<Command.Dialog bind:open>
    <Command.Input placeholder="Type a command or search..." />
    <Command.List>
        <Command.Empty>No results found.</Command.Empty>
        {#each Object.entries(groupedRoutes) as [group, items], index (group)}
            <Command.Group heading={group}>
                {#each items as route (route.href)}
                    <Command.Item>
                        <A class="flex gap-x-2" href={route.href} onclick={closeCommandWindow} target={route.openInNewTab ? '_blank' : undefined}>
                            {#if route.icon}
                                {@const Icon = route.icon}
                                <Icon />
                            {/if}
                            <div>{route.title}</div>
                        </A>
                    </Command.Item>
                {/each}
            </Command.Group>
            {#if index !== Object.keys(groupedRoutes).length - 1}
                <Command.Separator />
            {/if}
        {/each}
    </Command.List>
</Command.Dialog>
