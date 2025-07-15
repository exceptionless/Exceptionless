import type { FetchClientResponse, ProblemDetails } from '@exceptionless/fetchclient';
import type { CreateQueryResult } from '@tanstack/svelte-query';

import NumberFormatter from '$comp/formatters/number.svelte';
import OrganizationsActionsCell from '$features/organizations/components/table/organization-actions-cell.svelte';
import { ViewOrganization } from '$features/organizations/models';
import { getSharedTableOptions, type TableMemoryPagingParameters } from '$features/shared/table.svelte';
import { type ColumnDef, renderComponent } from '@tanstack/svelte-table';

import type { GetOrganizationsMode, GetOrganizationsParams } from '../../api.svelte';

export function getColumns<TOrganizations extends ViewOrganization>(mode: GetOrganizationsMode = 'stats'): ColumnDef<TOrganizations>[] {
    const columns: ColumnDef<TOrganizations>[] = [
        {
            accessorKey: 'name',
            cell: (info) => info.getValue(),
            enableHiding: false,
            header: 'Name',
            meta: {
                class: 'w-[200px]'
            }
        },
        {
            accessorKey: 'plan_name',
            cell: (info) => info.getValue(),
            enableHiding: false,
            enableSorting: false,
            header: 'Plan',
            meta: {
                class: 'w-[200px]'
            }
        }
    ];

    const isStatsMode = mode === 'stats';
    if (isStatsMode) {
        columns.push(
            {
                accessorKey: 'project_count',
                cell: (info) => renderComponent(NumberFormatter, { value: info.getValue<number>() }),
                enableSorting: false,
                header: 'Projects',
                meta: {
                    class: 'w-24'
                }
            },
            {
                accessorKey: 'stack_count',
                cell: (info) => renderComponent(NumberFormatter, { value: info.getValue<number>() }),
                enableSorting: false,
                header: 'Stacks',
                meta: {
                    class: 'w-24'
                }
            },
            {
                accessorKey: 'event_count',
                cell: (info) => renderComponent(NumberFormatter, { value: info.getValue<number>() }),
                enableSorting: false,
                header: 'Events',
                meta: {
                    class: 'w-24'
                }
            }
        );
    }

    columns.push({
        cell: (info) => renderComponent(OrganizationsActionsCell, { organization: info.row.original }),
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

export function getTableOptions<TOrganizations extends ViewOrganization>(
    queryParameters: GetOrganizationsParams & TableMemoryPagingParameters,
    queryResponse: CreateQueryResult<FetchClientResponse<TOrganizations[]>, ProblemDetails>
) {
    return getSharedTableOptions<TOrganizations>({
        columnPersistenceKey: 'organizations-column-visibility',
        get columns() {
            return getColumns<TOrganizations>(queryParameters.mode);
        },
        paginationStrategy: 'memory',
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
