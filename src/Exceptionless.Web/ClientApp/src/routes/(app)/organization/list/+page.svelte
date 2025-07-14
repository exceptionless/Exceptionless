<script lang="ts">
    import type { ViewOrganization } from '$features/organizations/models';
    import type { TableMemoryPagingParameters } from '$features/shared/table.svelte';

    import { goto } from '$app/navigation';
    import { Button } from '$comp/ui/button';
    import * as Card from '$comp/ui/card';
    import { type GetOrganizationsParams, getOrganizationsQuery } from '$features/organizations/api.svelte';
    import { getTableOptions } from '$features/organizations/components/table/options.svelte';
    import OrganizationsDataTable from '$features/organizations/components/table/organizations-data-table.svelte';
    import Plus from '@lucide/svelte/icons/plus';
    import { createTable } from '@tanstack/svelte-table';

    const organizationsQueryParameters: GetOrganizationsParams & TableMemoryPagingParameters = $state({
        mode: 'stats'
    });

    const organizationsQuery = getOrganizationsQuery({
        get params() {
            return organizationsQueryParameters;
        }
    });

    const table = createTable(getTableOptions<ViewOrganization>(organizationsQueryParameters, organizationsQuery));

    async function rowClick(organization: ViewOrganization) {
        if (organization.id) {
            await goto(`/next/organization/${organization.id}/manage`);
        }
    }

    async function addOrganization() {
        await goto('/next/organization/add');
    }
</script>

<div class="flex flex-col space-y-4">
    <Card.Root>
        <Card.Header>
            <Card.Title class="text-2xl">My Organizations</Card.Title>
            <Card.Description>View and manage your organizations. Click on an organization to view its details.</Card.Description>
            <Card.Action>
                <Button size="icon" onclick={addOrganization} title="Add Organization">
                    <Plus class="size-4" aria-hidden="true" />
                    <span class="sr-only">Add Organization</span>
                </Button>
            </Card.Action>
        </Card.Header>
        <Card.Content>
            <OrganizationsDataTable isLoading={organizationsQuery.isLoading} {rowClick} {table} />
        </Card.Content>
    </Card.Root>
</div>
