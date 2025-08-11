<script lang="ts">
    import { goto } from '$app/navigation';
    import { page } from '$app/state';
    import { H3, Muted } from '$comp/typography';
    import { Button } from '$comp/ui/button';
    import { Separator } from '$comp/ui/separator';
    import { organization } from '$features/organizations/context.svelte';
    import { getProjectQuery } from '$features/projects/api.svelte';
    import * as SplitLayout from '$features/shared/components/layouts/split-layout';
    import NotificationSettings from '@lucide/svelte/icons/mail';
    import { toast } from 'svelte-sonner';

    import SidebarNav from '../../(components)/sidebar-nav.svelte';
    import { routes } from './routes.svelte';

    let { children } = $props();

    const projectId = page.params.projectId || '';
    const projectQuery = getProjectQuery({
        route: {
            get id() {
                return projectId;
            }
        }
    });

    $effect(() => {
        if (projectQuery.isError) {
            toast.error(`The project "${projectId}" could not be found.`);
            goto('/next/project/list');
        }

        if (projectQuery.isSuccess && projectQuery.data.organization_id !== organization.current) {
            toast.error(`The project "${projectQuery.data.name}" does not belong to the current organization.`);
            goto('/next/project/list');
        }
    });
</script>

<div>
    <div class="flex flex-wrap items-start justify-between gap-4">
        <div class="flex flex-col gap-1">
            <H3 class="flex items-center gap-1">
                {#if projectQuery.isSuccess}
                    <div class="max-w-[70%] overflow-hidden" title={projectQuery.data.name}>
                        <span class="block truncate">{projectQuery.data.name}</span>
                    </div>
                {/if}
                <span class="shrink-0">Settings</span>
            </H3>
            <Muted>Manage your project settings and integrations.</Muted>
        </div>
        <div class="flex items-center gap-2">
            <Button variant="secondary" size="icon" href="/account/manage?tab=notifications&projectId={projectId}" title="Notification Settings">
                <NotificationSettings class="size-4" />
            </Button>
        </div>
    </div>
    <Separator class="mx-6 my-6 w-auto" />
    <SplitLayout.Root>
        <SplitLayout.Sidebar>
            <SidebarNav routes={routes()} />
        </SplitLayout.Sidebar>
        <SplitLayout.Content>
            {@render children()}
        </SplitLayout.Content>
    </SplitLayout.Root>
</div>
