<script lang="ts">
    import A from '$comp/typography/A.svelte';
    import * as Command from '$comp/ui/command';
    import { isCommandOpen } from '$lib/stores/app';
    import type { NavigationItem } from '../routes';

    export let open: boolean;
    export let routes: NavigationItem[];
    const groupedRoutes: Record<string, NavigationItem[]> = Object.groupBy(routes, (item: NavigationItem) => item.group);

    function closeCommandWindow() {
        isCommandOpen.set(false);
    }
</script>

<Command.Dialog bind:open>
    <Command.Input placeholder="Type a command or search..." />
    <Command.List>
        <Command.Empty>No results found.</Command.Empty>
        {#each Object.entries(groupedRoutes) as [group, items], index}
            <Command.Group heading={group}>
                {#each items as route}
                    <Command.Item>
                        <A href={route.href} class="flex gap-x-2" on:click={closeCommandWindow}>
                            <svelte:component this={route.icon} />
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
