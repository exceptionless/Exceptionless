import type { FetchClientResponse, ProblemDetails } from '@exceptionless/fetchclient';
import type { CreateQueryResult } from '@tanstack/svelte-query';

import BooleanFormatter from '$features/shared/components/formatters/boolean.svelte';
import { getSharedTableOptions } from '$features/shared/table.svelte';
import UserActionsCell from '$features/users/components/table/user-actions-cell.svelte';
import { ViewUser } from '$features/users/models';
import { type ColumnDef, renderComponent } from '@tanstack/svelte-table';

import type { GetOrganizationUsersParams } from '../../api.svelte';

export function getColumns<TUser extends ViewUser>(organizationId: string): ColumnDef<TUser>[] {
    const columns: ColumnDef<TUser>[] = [
        {
            accessorKey: 'email_address',
            cell: (info) => info.getValue(),
            enableHiding: false,
            enableSorting: false,
            header: 'Email Address',
            meta: {
                class: 'w-[200px]'
            }
        },
        {
            accessorKey: 'full_name',
            cell: (info) => info.getValue(),
            enableHiding: false,
            enableSorting: false,
            header: 'Name',
            meta: {
                class: 'w-[200px]'
            }
        },
        {
            accessorKey: 'is_active',
            cell: (info) => renderComponent(BooleanFormatter, { value: info.getValue() as boolean }),
            enableHiding: true,
            enableSorting: false,
            header: 'Active',
            meta: {
                class: 'w-[100px]'
            }
        },
        {
            accessorKey: 'is_invite',
            cell: (info) => renderComponent(BooleanFormatter, { value: info.getValue() as boolean }),
            enableHiding: true,
            enableSorting: false,
            header: 'Invited',
            meta: {
                class: 'w-[100px]'
            }
        }
    ];

    columns.push({
        cell: (info) => renderComponent(UserActionsCell, { organizationId, user: info.row.original }),
        enableHiding: false,
        enableSorting: false,
        header: 'Actions',
        id: 'actions',
        meta: {
            class: 'w-16'
        }
    });

    return columns;
}

export function getTableOptions<TUser extends ViewUser>(
    queryParameters: GetOrganizationUsersParams,
    queryResponse: CreateQueryResult<FetchClientResponse<TUser[]>, ProblemDetails>,
    organizationId: string
) {
    return getSharedTableOptions<TUser>({
        columnPersistenceKey: 'users-column-visibility',
        get columns() {
            return getColumns<TUser>(organizationId);
        },
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
