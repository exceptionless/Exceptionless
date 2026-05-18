<script lang="ts">
    import { goto } from '$app/navigation';
    import { resolve } from '$app/paths';
    import { page } from '$app/state';
    import { A, H3 } from '$comp/typography';
    import { Button } from '$comp/ui/button';
    import { getOrganizationQuery } from '$features/organizations/api.svelte';
    import OrganizationAdminActionsDropdownMenu from '$features/organizations/components/organization-admin-actions-dropdown-menu.svelte';
    import { organization } from '$features/organizations/context.svelte';
    import { getProjectQuery } from '$features/projects/api.svelte';
    import GlobalUser from '$features/users/components/global-user.svelte';
    import NotificationSettings from '@lucide/svelte/icons/mail';
    import { toast } from 'svelte-sonner';

    import { routes } from './routes.svelte';

    let { children } = $props();

    const projectId = $derived(page.params.projectId || '');
    const projectQuery = getProjectQuery({
        route: {
            get id() {
                return projectId;
            }
        }
    });

    const organizationQuery = getOrganizationQuery({
        route: {
            get id() {
                return organization.current;
            }
        }
    });
    const currentPath = $derived(page.url.pathname);

    $effect(() => {
        if (projectQuery.isError) {
            toast.error(`The project "${projectId}" could not be found.`);
            if (organization.current) {
                goto(resolve('/(app)/organization/[organizationId]/projects', { organizationId: organization.current }));
            } else {
                goto(resolve('/(app)/organization/list'));
            }
        }

        if (projectQuery.isSuccess && projectQuery.data.organization_id !== organization.current) {
            toast.error(`The project "${projectQuery.data.name}" does not belong to the current organization.`);
            if (organization.current) {
                goto(resolve('/(app)/organization/[organizationId]/projects', { organizationId: organization.current }));
            } else {
                goto(resolve('/(app)/organization/list'));
            }
        }
    });
</script>

<div>
    <div class="flex flex-wrap items-start justify-between gap-4">
        <div class="flex flex-col gap-1">
            <H3 class="flex flex-wrap items-center gap-x-1">
                {#if projectQuery.isSuccess}
                    <span>{projectQuery.data.name}</span>
                {/if}
                <span class="shrink-0">Settings</span>
            </H3>
        </div>
        <div class="flex items-center gap-2">
            {#if organizationQuery.isSuccess}
                <GlobalUser>
                    <OrganizationAdminActionsDropdownMenu organization={organizationQuery.data} />
                </GlobalUser>
            {/if}
            <Button variant="secondary" size="icon" href={`${resolve('/(app)/account/notifications')}?project=${projectId}`} title="Notification Settings">
                <NotificationSettings class="size-4" />
            </Button>
        </div>
    </div>
    <div class="mt-6 space-y-6">
        <nav class="bg-muted flex w-full flex-row flex-nowrap gap-1 overflow-x-auto rounded-lg p-1">
            {#each routes() as route (route.href)}
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
