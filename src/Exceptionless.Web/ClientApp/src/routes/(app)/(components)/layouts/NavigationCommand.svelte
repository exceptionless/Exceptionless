<script lang="ts">
    import * as Command from '$comp/ui/command';
    import type { NavigationItem } from './routes';

    let open = false;
    export let routes: NavigationItem[];
    const groupedRoutes: Record<string, NavigationItem[]> = Object.groupBy(routes, (item: NavigationItem) => item.group);

    function showCommandList() {
        open = true;
    }

    function hideCommandList() {
        open = false;
    }
</script>

<Command.Root>
    <Command.Input placeholder="Type a command or search..." on:focus={showCommandList} on:blur={hideCommandList} />
    {#if open}
        <Command.List>
            <Command.Empty>No results found.</Command.Empty>
            {#each Object.entries(groupedRoutes) as [group, items], index}
                <Command.Group heading={group}>
                    {#each items as route}
                        <Command.Item>
                            <a href={route.href} class="flex gap-x-2">
                                <svelte:component this={route.icon} />
                                <div>{route.title}</div>
                            </a>
                        </Command.Item>
                    {/each}
                </Command.Group>
                {#if index !== Object.keys(groupedRoutes).length - 1}
                    <Command.Separator />
                {/if}
            {/each}
        </Command.List>
    {/if}
</Command.Root>
