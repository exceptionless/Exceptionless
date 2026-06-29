import type { OAuthGrant } from '$features/users/models';
import type { ProblemDetails } from '@exceptionless/fetchclient';
import type { CreateQueryResult } from '@tanstack/svelte-query';

import { getSharedTableOptions, type TableMemoryPagingParameters } from '$features/shared/table.svelte';
import OAuthGrantAccessCell from '$features/users/components/oauth-grants/table/oauth-grant-access-cell.svelte';
import OAuthGrantActionsCell from '$features/users/components/oauth-grants/table/oauth-grant-actions-cell.svelte';
import OAuthGrantApplicationCell from '$features/users/components/oauth-grants/table/oauth-grant-application-cell.svelte';
import { type ColumnDef, renderComponent, type StockFeatures } from '@tanstack/svelte-table';

export function getColumns(organizationNamesById: ReadonlyMap<string, string>): ColumnDef<StockFeatures, OAuthGrant, unknown>[] {
    const columns: ColumnDef<StockFeatures, OAuthGrant, unknown>[] = [
        {
            accessorKey: 'application_name',
            cell: (info) => renderComponent(OAuthGrantApplicationCell, { grant: info.row.original }),
            enableHiding: false,
            enableSorting: false,
            header: 'Application',
            meta: {
                class: 'w-[22rem] min-w-[14rem] max-w-none whitespace-normal align-top'
            }
        },
        {
            accessorKey: 'resources',
            cell: (info) => renderComponent(OAuthGrantAccessCell, { grant: info.row.original, organizationNamesById }),
            enableHiding: false,
            enableSorting: false,
            header: 'Access',
            meta: {
                class: 'w-auto max-w-none whitespace-normal align-top'
            }
        },
        {
            cell: (info) => renderComponent(OAuthGrantActionsCell, { grant: info.row.original }),
            enableHiding: false,
            enableSorting: false,
            header: '',
            id: 'actions',
            meta: {
                class: 'w-12 min-w-12 max-w-12 text-right align-top'
            }
        }
    ];

    return columns;
}

export function getTableOptions(
    queryParameters: TableMemoryPagingParameters,
    queryResponse: CreateQueryResult<OAuthGrant[], ProblemDetails>,
    getOrganizationNamesById: () => ReadonlyMap<string, string>
) {
    return getSharedTableOptions<OAuthGrant, 'memory'>({
        columnPersistenceKey: 'oauth-grants-compact',
        get columns() {
            return getColumns(getOrganizationNamesById());
        },
        paginationStrategy: 'memory',
        get queryData() {
            return queryResponse.data ?? [];
        },
        get queryParameters() {
            return queryParameters;
        }
    });
}
