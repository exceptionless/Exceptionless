<script lang="ts">
    import { goto } from '$app/navigation';
    import { page } from '$app/state';
    import * as Card from '$comp/ui/card';
    import { Separator } from '$comp/ui/separator';
    import { getOrganizationQuery } from '$features/organizations/api.svelte';
    import { organization } from '$features/organizations/context.svelte';
    import * as SplitLayout from '$features/shared/components/layouts/split-layout';
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

    $effect(() => {
        if (organizationQuery.isError) {
            toast.error(`The organization "${organizationId}" could not be found.`);
            goto('/next/organization/list');
        }

        if (organizationQuery.isSuccess && organizationQuery.data.id !== organization.current) {
            organization.current = organizationQuery.data.id;
        }
    });
</script>

<Card.Root>
    <Card.Header>
        <Card.Title class="flex items-center gap-1 text-2xl"
            >{#if organizationQuery.isSuccess}
                <div class="max-w-[70%] overflow-hidden" title={organizationQuery.data.name}>
                    <span class="block truncate">{organizationQuery.data.name}</span>
                </div>
            {/if}
            <span class="shrink-0">Settings</span></Card.Title
        >
        <Card.Description>Manage your organization's settings, users, and billing information.</Card.Description>
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
