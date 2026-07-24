<script lang="ts">
    import { goto } from '$app/navigation';
    import { resolve } from '$app/paths';
    import { page } from '$app/state';
    import { A, H3 } from '$comp/typography';
    import { accessToken } from '$features/auth/index.svelte';
    import { getOrganizationQuery } from '$features/organizations/api.svelte';
    import OrganizationAdminActionsDropdownMenu from '$features/organizations/components/organization-admin-actions-dropdown-menu.svelte';
    import { organization } from '$features/organizations/context.svelte';
    import { getMeQuery } from '$features/users/api.svelte';
    import GlobalUser from '$features/users/components/global-user.svelte';
    import { toast } from 'svelte-sonner';

    import type { NavigationItemContext } from '../../../routes.svelte';

    import { routes } from './routes.svelte';

    let { children } = $props();

    const organizationId = $derived(page.params.organizationId || '');
    const meQuery = getMeQuery();
    const isAuthenticated = $derived(accessToken.current !== null);
    const organizationQuery = getOrganizationQuery({
        route: {
            get id() {
                return organizationId;
            }
        }
    });

    const filteredRoutes = $derived.by(() => {
        const context: NavigationItemContext = { authenticated: isAuthenticated, user: meQuery.data };
        return routes().filter((route) => route.group === 'Organization Settings' && (route.show ? route.show(context) : true));
    });
    const currentPath = $derived(page.url.pathname);

    $effect(() => {
        if (organizationQuery.isError) {
            toast.error(`The organization "${organizationId}" could not be found.`);
            goto(resolve('/(app)/organization/list'));
            return;
        }

        if (organizationQuery.isSuccess && organization.current && organizationId !== organization.current) {
            goto(page.url.pathname.replace(`/organization/${organizationId}`, `/organization/${organization.current}`));
            return;
        }
    });
</script>

<div>
    <div class="flex items-start justify-between">
        <div class="flex flex-col gap-1">
            <H3 class="flex flex-wrap items-center gap-x-1">
                {#if organizationQuery.isSuccess}
                    <span>{organizationQuery.data.name}</span>
                {/if}
                <span class="shrink-0">Settings</span>
            </H3>
        </div>
        {#if organizationQuery.isSuccess}
            <GlobalUser>
                <OrganizationAdminActionsDropdownMenu organization={organizationQuery.data} />
            </GlobalUser>
        {/if}
    </div>
    <div class="mt-6 space-y-6">
        <nav class="flex w-full flex-row flex-nowrap gap-1 overflow-x-auto rounded-lg bg-muted p-1">
            {#each filteredRoutes as route (route.href)}
                {@const isActive = currentPath === route.href || currentPath.startsWith(route.href + '/')}
                <A
                    variant="ghost"
                    href={route.href}
                    data-sveltekit-noscroll
                    class="shrink-0 rounded-md px-3 py-1.5 text-sm font-medium transition-colors {isActive
                        ? 'bg-background text-foreground shadow-xs'
                        : 'text-muted-foreground hover:bg-muted hover:text-foreground'}"
                >
                    {route.title}
                </A>
            {/each}
        </nav>
        {@render children()}
    </div>
</div>
