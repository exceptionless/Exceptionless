<script lang="ts">
    import type { ViewOrganization } from '$features/organizations/models';
    import type { TableMemoryPagingParameters } from '$features/shared/table.svelte';

    import { goto } from '$app/navigation';
    import { resolve } from '$app/paths';
    import DataTableViewOptions from '$comp/data-table/data-table-view-options.svelte';
    import { H3 } from '$comp/typography';
    import { Button } from '$comp/ui/button';
    import { type GetOrganizationsParams, getOrganizationsQuery } from '$features/organizations/api.svelte';
    import { getTableOptions } from '$features/organizations/components/table/options.svelte';
    import OrganizationsDataTable from '$features/organizations/components/table/organizations-data-table.svelte';
    import { organization } from '$features/organizations/context.svelte';
    import { useHideOrganizationNotifications } from '$features/organizations/hooks/use-hide-organization-notifications.svelte';
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

    async function rowClick(org: ViewOrganization) {
        if (org.id) {
            organization.current = org.id;
            await goto(resolve('/(app)/organization/[organizationId]/manage', { organizationId: org.id }));
        }
    }

    function rowHref(org: ViewOrganization): string {
        return resolve('/(app)/organization/[organizationId]/manage', { organizationId: org.id });
    }

    async function addOrganization() {
        await goto(resolve('/(app)/organization/add'));
    }

    useHideOrganizationNotifications();
</script>

<div class="flex flex-col gap-4">
    <div class="flex flex-wrap items-start justify-between gap-4">
        <div class="flex flex-col gap-1">
            <H3>My Organizations</H3>
        </div>
    </div>
    <OrganizationsDataTable isLoading={organizationsQuery.isLoading} {rowClick} {rowHref} {table}>
        {#snippet toolbarChildren()}
            <div class="flex-1"></div>
            <DataTableViewOptions size="icon-lg" {table} />
            <Button size="icon-lg" onclick={addOrganization} title="Add Organization">
                <Plus class="size-4" aria-hidden="true" />
                <span class="sr-only">Add Organization</span>
            </Button>
        {/snippet}
    </OrganizationsDataTable>
</div>
