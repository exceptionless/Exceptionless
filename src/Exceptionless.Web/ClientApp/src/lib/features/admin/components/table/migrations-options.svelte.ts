import type { MigrationState, MigrationStatus } from '$features/admin/models';

import DateTime from '$comp/formatters/date-time.svelte';
import { getSharedTableOptions, type TableMemoryPagingParameters } from '$features/shared/table.svelte';
import { type ColumnDef, createSortedRowModel, renderComponent, sortFns, type StockFeatures } from '@tanstack/svelte-table';

import MigrationDurationCell from './migration-duration-cell.svelte';
import MigrationErrorCell from './migration-error-cell.svelte';
import MigrationStatusCell from './migration-status-cell.svelte';
import MigrationTypeCell from './migration-type-cell.svelte';

export type MigrationStateRow = MigrationState & { status: MigrationStatus };

export function getColumns(): ColumnDef<StockFeatures, MigrationStateRow, unknown>[] {
    return [
        {
            accessorKey: 'id',
            cell: (info) => info.getValue(),
            enableSorting: true,
            header: 'Name',
            meta: {
                class: 'max-w-xs font-medium'
            }
        },
        {
            accessorKey: 'version',
            cell: (info) => (info.getValue() as null | number) ?? '—',
            enableSorting: true,
            header: 'Version',
            meta: {
                class: 'text-right'
            }
        },
        {
            accessorKey: 'migration_type',
            cell: (info) => renderComponent(MigrationTypeCell, { value: info.getValue() as number }),
            enableSorting: true,
            header: 'Type'
        },
        {
            accessorKey: 'status',
            cell: (info) => renderComponent(MigrationStatusCell, { status: info.getValue() as MigrationStatus }),
            enableSorting: true,
            header: 'Status'
        },
        {
            accessorKey: 'started_utc',
            cell: (info) => renderComponent(DateTime, { value: (info.getValue() as null | string) ?? undefined }),
            enableSorting: true,
            header: 'Started'
        },
        {
            accessorKey: 'completed_utc',
            cell: (info) => renderComponent(DateTime, { value: (info.getValue() as null | string) ?? undefined }),
            enableSorting: true,
            header: 'Completed'
        },
        {
            cell: ({ row }) =>
                renderComponent(MigrationDurationCell, {
                    completedUtc: row.original.completed_utc,
                    startedUtc: row.original.started_utc
                }),
            enableSorting: false,
            header: 'Duration',
            id: 'duration'
        },
        {
            accessorKey: 'error_message',
            cell: (info) => renderComponent(MigrationErrorCell, { value: info.getValue() as null | string | undefined }),
            enableSorting: false,
            header: 'Error',
            meta: {
                class: 'max-w-xs'
            }
        }
    ];
}

export function getTableOptions(queryParameters: TableMemoryPagingParameters, getData: () => MigrationStateRow[]) {
    return getSharedTableOptions<MigrationStateRow, 'memory'>({
        columnPersistenceKey: 'admin-migrations',
        get columns() {
            return getColumns();
        },
        configureOptions: (options) => {
            options.getRowId = (row) => row.id;
            options._rowModels = { ...options._rowModels, sortedRowModel: createSortedRowModel(sortFns) };
            options.initialState = {
                sorting: [{ desc: true, id: 'version' }]
            };
            options.manualSorting = false;
            return options;
        },
        defaultColumnVisibility: {
            completed_utc: false,
            duration: false,
            error_message: false
        },
        paginationStrategy: 'memory',
        get queryData() {
            return getData();
        },
        get queryParameters() {
            return queryParameters;
        }
    });
}
