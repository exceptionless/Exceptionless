<script lang="ts">
    import type { ViewOrganization } from '$features/organizations/models';
    import type { TableMemoryPagingParameters } from '$features/shared/table.svelte';

    import { goto } from '$app/navigation';
    import { H3, Muted } from '$comp/typography';
    import { Button } from '$comp/ui/button';
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

<div class="flex flex-col gap-4">
    <div class="flex flex-wrap items-start justify-between gap-4">
        <div class="flex flex-col gap-1">
            <H3>My Organizations</H3>
            <Muted>View and manage your organizations. Click on an organization to view its details.</Muted>
        </div>
        <div class="flex items-center gap-2">
            <Button size="icon" onclick={addOrganization} title="Add Organization">
                <Plus class="size-4" aria-hidden="true" />
                <span class="sr-only">Add Organization</span>
            </Button>
        </div>
    </div>
    <OrganizationsDataTable isLoading={organizationsQuery.isLoading} {rowClick} {table} />
</div>
