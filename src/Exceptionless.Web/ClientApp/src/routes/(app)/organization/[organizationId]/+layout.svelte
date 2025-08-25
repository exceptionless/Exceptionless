<script lang="ts">
    import { goto } from '$app/navigation';
    import { page } from '$app/state';
    import { H3, Muted } from '$comp/typography';
    import { Separator } from '$comp/ui/separator';
    import { getOrganizationQuery } from '$features/organizations/api.svelte';
    import OrganizationAdminActionsDropdownMenu from '$features/organizations/components/organization-admin-actions-dropdown-menu.svelte';
    import { organization } from '$features/organizations/context.svelte';
    import * as SplitLayout from '$features/shared/components/layouts/split-layout';
    import GlobalUser from '$features/users/components/global-user.svelte';
    import { toast } from 'svelte-sonner';

    import SidebarNav from '../../(components)/sidebar-nav.svelte';
    import { routes } from './routes.svelte';

    let { children } = $props();

    const organizationId = page.params.organizationId || '';
    const organizationQuery = getOrganizationQuery({
        route: {
            get id() {
                return organizationId;
            }
        }
    });

    // Set the current organization based on the current organization param on page load.
    if (organizationId !== organization.current) {
        organization.current = organizationId;
    }

    const filteredRoutes = $derived(routes().filter((route) => route.group === 'Organization Settings'));

    $effect(() => {
        if (organizationQuery.isError) {
            toast.error(`The organization "${organizationId}" could not be found.`);
            goto('/next/organization/list');
            return;
        }

        if (organizationQuery.isSuccess && organizationId !== organization.current) {
            goto(page.url.pathname.replace(`/organization/${organizationId}`, `/organization/${organization.current}`));
            return;
        }
    });
</script>

<div>
    <div class="flex items-start justify-between">
        <div class="flex flex-col gap-1">
            <H3 class="flex items-center gap-1">
                {#if organizationQuery.isSuccess}
                    <div class="max-w-[70%] overflow-hidden" title={organizationQuery.data.name}>
                        <span class="block truncate">{organizationQuery.data.name}</span>
                    </div>
                {/if}
                <span class="shrink-0">Settings</span>
            </H3>
            <Muted>Manage your organization's settings, users, and billing information.</Muted>
        </div>
        {#if organizationQuery.isSuccess}
            <GlobalUser>
                <OrganizationAdminActionsDropdownMenu organization={organizationQuery.data} />
            </GlobalUser>
        {/if}
    </div>
    <Separator class="mx-6 my-6 w-auto" />
    <SplitLayout.Root>
        <SplitLayout.Sidebar>
            <SidebarNav routes={filteredRoutes} />
        </SplitLayout.Sidebar>
        <SplitLayout.Content>
            {@render children()}
        </SplitLayout.Content>
    </SplitLayout.Root>
</div>
