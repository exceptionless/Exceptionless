<script lang="ts">
    import { derived } from 'svelte/store';
    import { isAuthenticated } from '$api/auth';
    import { isSidebarOpen, isLargeScreen } from '$lib/stores/app';
    import SidebarMenuItem from './SidebarMenuItem.svelte';
    import { getMeQuery } from '$api/queries/users';
    import { routes } from '../../routes';
    import type { NavigationItemContext } from '../../../routes';

    function onBackdropClick() {
        isSidebarOpen.set(false);
    }

    const userQuery = getMeQuery();
    const derivedRoutes = derived(userQuery, ($userResponse) => {
        const context: NavigationItemContext = { authenticated: $isAuthenticated, user: $userResponse.data };
        return routes.filter((route) => route.group === 'Dashboards' && (route.show ? route.show(context) : true));
    });
</script>

<aside
    id="sidebar"
    class="transition-width fixed left-0 top-0 z-20 flex h-full w-64 flex-shrink-0 flex-col bg-background pt-16 text-foreground duration-75 lg:flex {$isSidebarOpen
        ? 'lg:w-64'
        : 'hidden lg:w-16'}"
    aria-label="Sidebar"
>
    <div class="relative flex min-h-0 flex-1 flex-col border-r pt-0" role="none">
        <div class="flex flex-1 flex-col overflow-y-auto pb-4 pt-5">
            <div class="flex-1 space-y-1 divide-y px-3">
                <ul class="space-y-2 pb-2">
                    {#each $derivedRoutes as route}
                        <li>
                            <SidebarMenuItem title={route.title} href={route.href}>
                                <span slot="icon" let:iconClass>
                                    <svelte:component this={route.icon} class={iconClass} />
                                </span>
                            </SidebarMenuItem>
                        </li>
                    {/each}
                </ul>
            </div>
        </div>
    </div>
</aside>

<button
    class="fixed inset-0 z-10 bg-gray-900/50 dark:bg-gray-900/90 {!$isLargeScreen && $isSidebarOpen ? '' : 'hidden'}"
    title="Close sidebar"
    aria-label="Close sidebar"
    on:click={onBackdropClick}
></button>
