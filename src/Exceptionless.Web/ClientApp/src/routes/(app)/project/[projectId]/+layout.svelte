<script lang="ts">
    import { goto } from '$app/navigation';
    import { page } from '$app/state';
    import { Button } from '$comp/ui/button';
    import * as Card from '$comp/ui/card';
    import { Separator } from '$comp/ui/separator';
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
    });
</script>

<Card.Root>
    <Card.Header>
        <Card.Title class="text-2xl" level={2}
            >{#if projectQuery.isSuccess}{projectQuery.data.name}
            {/if} Settings</Card.Title
        >
        <Card.Description>Manage your project settings and integrations.</Card.Description>
        <Card.Action>
            <Button variant="secondary" size="icon" href="/account/manage?tab=notifications&projectId={projectId}" title="Notification Settings">
                <NotificationSettings class="size-4" />
            </Button>
        </Card.Action>
    </Card.Header>
    <Separator class="mx-6 my-6 w-auto" />

    <Card.Content>
        <SplitLayout.Root>
            <SplitLayout.Sidebar>
                <SidebarNav routes={routes()} />
            </SplitLayout.Sidebar>
            <SplitLayout.Content>
                {@render children()}
            </SplitLayout.Content>
        </SplitLayout.Root>
    </Card.Content>
</Card.Root>
