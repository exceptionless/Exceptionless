import type { GetOAuthApplicationsParams } from '$features/admin/api.svelte';
import type { OAuthApplication } from '$features/admin/models';
import type { FetchClientResponse, ProblemDetails } from '@foundatiofx/fetchclient';
import type { CreateQueryResult } from '@tanstack/svelte-query';

import { getSharedTableOptions } from '$features/shared/table.svelte';
import { type ColumnDef, renderComponent, type StockFeatures } from '@tanstack/svelte-table';

import OAuthApplicationActionsCell from './oauth-application-actions-cell.svelte';
import OAuthApplicationExpandCell from './oauth-application-expand-cell.svelte';
import OAuthApplicationOrganizationsCell from './oauth-application-organizations-cell.svelte';
import OAuthApplicationScopesCell from './oauth-application-scopes-cell.svelte';
import OAuthApplicationSummaryCell from './oauth-application-summary-cell.svelte';

export function getColumns(): ColumnDef<StockFeatures, OAuthApplication, unknown>[] {
    return [
        {
            cell: (info) =>
                renderComponent(OAuthApplicationExpandCell, {
                    row: info.row
                }),
            enableHiding: false,
            enableSorting: false,
            header: '',
            id: 'expand',
            meta: {
                class: 'w-12 min-w-12 max-w-12'
            }
        },
        {
            accessorKey: 'name',
            cell: (info) =>
                renderComponent(OAuthApplicationSummaryCell, {
                    application: info.row.original
                }),
            enableHiding: false,
            enableSorting: false,
            header: 'Application',
            meta: {
                class: 'w-[45%] max-w-none whitespace-normal'
            }
        },
        {
            accessorKey: 'organizations',
            cell: (info) =>
                renderComponent(OAuthApplicationOrganizationsCell, {
                    application: info.row.original
                }),
            enableSorting: false,
            header: 'Authorized organizations',
            meta: {
                class: 'w-56 max-w-none whitespace-normal'
            }
        },
        {
            accessorKey: 'scopes',
            cell: (info) =>
                renderComponent(OAuthApplicationScopesCell, {
                    application: info.row.original
                }),
            enableSorting: false,
            header: 'Access',
            meta: {
                class: 'w-28 max-w-none'
            }
        },
        {
            cell: (info) =>
                renderComponent(OAuthApplicationActionsCell, {
                    application: info.row.original
                }),
            enableHiding: false,
            enableSorting: false,
            header: '',
            id: 'actions',
            meta: {
                class: 'w-12 min-w-12 max-w-12 text-right'
            }
        }
    ];
}

export function getTableOptions(
    queryParameters: GetOAuthApplicationsParams,
    queryResponse: CreateQueryResult<FetchClientResponse<OAuthApplication[]>, ProblemDetails>
) {
    return getSharedTableOptions<OAuthApplication, 'offset'>({
        columnPersistenceKey: 'oauth-applications-details',
        columns: getColumns(),
        configureOptions: (options) => ({
            ...options,
            getRowCanExpand: () => true
        }),
        paginationStrategy: 'offset',
        get queryData() {
            return queryResponse.data?.data ?? [];
        },
        get queryMeta() {
            return queryResponse.data?.meta;
        },
        get queryParameters() {
            return queryParameters;
        }
    });
}
