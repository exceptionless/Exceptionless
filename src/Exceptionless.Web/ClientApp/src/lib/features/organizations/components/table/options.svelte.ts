import type { ViewOrganization } from '$features/organizations/models';
import type { FetchClientResponse, ProblemDetails } from '@exceptionless/fetchclient';
import type { CreateQueryResult } from '@tanstack/svelte-query';

import NumberFormatter from '$comp/formatters/number.svelte';
import { getSharedTableOptions, type TableMemoryPagingParameters } from '$features/shared/table.svelte';
import { type ColumnDef, renderComponent } from '@tanstack/svelte-table';

import type { GetOrganizationsMode, GetOrganizationsParams } from '../../api.svelte';

import OrganizationsActionsCell from './organization-actions-cell.svelte';
import OrganizationOverLimitCell from './organization-over-limit-cell.svelte';
import OrganizationRetentionDaysCell from './organization-retention-days-cell.svelte';
import OrganizationSuspensionCell from './organization-suspension-cell.svelte';

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
        },
        {
            accessorFn: (row) => row.is_over_monthly_limit,
            cell: (info) => renderComponent(OrganizationOverLimitCell, { isOverLimit: info.getValue<boolean>() }),
            enableSorting: false,
            header: 'Over Limit',
            id: 'is_over_monthly_limit',
            meta: {
                class: 'w-24',
                defaultHidden: true
            }
        },
        {
            accessorKey: 'retention_days',
            cell: (info) => renderComponent(OrganizationRetentionDaysCell, { value: info.getValue<number>() }),
            enableSorting: false,
            header: 'Retention',
            meta: {
                class: 'w-24',
                defaultHidden: true
            }
        },
        {
            accessorFn: (row) => row.is_suspended,
            cell: (info) => {
                const org = info.row.original;
                return renderComponent(OrganizationSuspensionCell, {
                    code: org.suspension_code,
                    notes: org.suspension_notes
                });
            },
            enableSorting: false,
            header: 'Suspended',
            id: 'is_suspended',
            meta: {
                class: 'w-28',
                defaultHidden: true
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
