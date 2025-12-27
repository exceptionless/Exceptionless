import type { ViewProject } from '$features/projects/models';
import type { FetchClientResponse, ProblemDetails } from '@exceptionless/fetchclient';
import type { CreateQueryResult } from '@tanstack/svelte-query';

import NumberFormatter from '$comp/formatters/number.svelte';
import ProjectActionsCell from '$features/projects/components/table/project-actions-cell.svelte';
import { getSharedTableOptions } from '$features/shared/table.svelte';
import { type ColumnDef, renderComponent } from '@tanstack/svelte-table';

import type { GetOrganizationProjectsParams, GetProjectsMode } from '../../api.svelte';

export function getColumns<TProject extends ViewProject>(mode: GetProjectsMode = 'stats'): ColumnDef<TProject>[] {
    const columns: ColumnDef<TProject>[] = [
        {
            accessorKey: 'name',
            cell: (info) => info.getValue(),
            enableHiding: false,
            header: 'Name',
            meta: {
                class: 'w-[200px]'
            }
        }
    ];

    const isStatsMode = mode === 'stats';
    if (isStatsMode) {
        columns.push(
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
        cell: (info) => renderComponent(ProjectActionsCell, { project: info.row.original }),
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

export function getTableOptions<TProject extends ViewProject>(
    queryParameters: GetOrganizationProjectsParams,
    queryResponse: CreateQueryResult<FetchClientResponse<TProject[]>, ProblemDetails>
) {
    return getSharedTableOptions<TProject>({
        columnPersistenceKey: 'projects-column-visibility',
        get columns() {
            return getColumns<TProject>(queryParameters.mode);
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
