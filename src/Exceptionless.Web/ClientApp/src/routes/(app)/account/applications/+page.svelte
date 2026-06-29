<script lang="ts">
    import type { TableMemoryPagingParameters } from '$features/shared/table.svelte';

    import DataTableViewOptions from '$comp/data-table/data-table-view-options.svelte';
    import { Muted, P } from '$comp/typography';
    import { getOrganizationsQuery } from '$features/organizations/api.svelte';
    import { DEFAULT_LIMIT } from '$features/shared/api/api.svelte';
    import { getOAuthGrantsQuery } from '$features/users/api.svelte';
    import OAuthGrantsDataTable from '$features/users/components/oauth-grants/table/oauth-grants-data-table.svelte';
    import { getTableOptions } from '$features/users/components/oauth-grants/table/options.svelte';
    import { createTable } from '@tanstack/svelte-table';
    import { queryParamsState } from 'kit-query-params';

    const grantsQuery = getOAuthGrantsQuery();
    const organizationsQuery = getOrganizationsQuery({});

    const organizations = $derived(organizationsQuery.data?.data ?? []);
    const organizationNamesById = $derived(
        new Map(organizations.filter((organization) => organization.id && organization.name).map((organization) => [organization.id!, organization.name!]))
    );

    const DEFAULT_PARAMS = {
        limit: DEFAULT_LIMIT,
        page: 1
    };

    const queryParams = queryParamsState({
        default: DEFAULT_PARAMS,
        pushHistory: true,
        schema: {
            limit: 'number',
            page: 'number'
        }
    });

    const grantQueryParameters: TableMemoryPagingParameters = $state({
        get limit() {
            return queryParams.limit!;
        },
        set limit(value) {
            queryParams.limit = value;
        },
        get page() {
            return queryParams.page!;
        },
        set page(value) {
            queryParams.page = value;
        }
    });

    const table = createTable(getTableOptions(grantQueryParameters, grantsQuery, () => organizationNamesById));

    $effect(() => {
        queryParams.limit ??= DEFAULT_LIMIT;
        queryParams.page ??= 1;
    });
</script>

<div class="space-y-6">
    <Muted>Manage applications connected to your account</Muted>

    {#if grantsQuery.isError}
        <P class="text-destructive py-8 text-sm">Failed to load applications.</P>
    {:else}
        <OAuthGrantsDataTable bind:limit={grantQueryParameters.limit!} isLoading={grantsQuery.isPending} {table}>
            {#snippet toolbarChildren()}
                <div class="flex-1"></div>
                <DataTableViewOptions size="icon-lg" {table} />
            {/snippet}
        </OAuthGrantsDataTable>
    {/if}
</div>
