import type { ElasticsearchSnapshot } from '$features/admin/models';

import DateTime from '$comp/formatters/date-time.svelte';
import Number from '$comp/formatters/number.svelte';
import { getSharedTableOptions, type TableMemoryPagingParameters } from '$features/shared/table.svelte';
import { type ColumnDef, createSortedRowModel, renderComponent, sortFns, type StockFeatures } from '@tanstack/svelte-table';

import ShardsCell from './shards-cell.svelte';
import SnapshotStatusCell from './snapshot-status-cell.svelte';

export function getColumns(): ColumnDef<StockFeatures, ElasticsearchSnapshot, unknown>[] {
    return [
        {
            accessorKey: 'name',
            cell: (info) => info.getValue(),
            enableSorting: true,
            header: 'Name',
            meta: {
                class: 'max-w-64 font-mono'
            }
        },
        {
            accessorKey: 'repository',
            cell: (info) => info.getValue(),
            enableSorting: true,
            header: 'Repository'
        },
        {
            accessorKey: 'status',
            cell: (info) => renderComponent(SnapshotStatusCell, { value: info.getValue() as string | undefined }),
            enableSorting: true,
            header: 'Status'
        },
        {
            accessorKey: 'start_time',
            cell: (info) => renderComponent(DateTime, { value: (info.getValue() as null | string) ?? undefined }),
            enableSorting: true,
            header: 'Started'
        },
        {
            accessorKey: 'duration',
            cell: (info) => info.getValue() ?? '—',
            enableSorting: false,
            header: 'Duration',
            meta: {
                class: 'font-mono'
            }
        },
        {
            accessorKey: 'indices_count',
            cell: (info) => renderComponent(Number, { value: info.getValue() as null | number }),
            enableSorting: true,
            header: 'Indices',
            meta: {
                class: 'text-right'
            }
        },
        {
            cell: ({ row }) =>
                renderComponent(ShardsCell, {
                    failedShards: row.original.failed_shards,
                    successfulShards: row.original.successful_shards,
                    totalShards: row.original.total_shards
                }),
            enableSorting: false,
            header: 'Shards',
            id: 'shards',
            meta: {
                class: 'text-right'
            }
        }
    ];
}

export function getTableOptions(queryParameters: TableMemoryPagingParameters, getData: () => ElasticsearchSnapshot[]) {
    return getSharedTableOptions<ElasticsearchSnapshot, 'memory'>({
        columnPersistenceKey: 'admin-backups',
        get columns() {
            return getColumns();
        },
        configureOptions: (options) => {
            options.getRowId = (row) => row.repository + '/' + row.name;
            options._rowModels = { ...options._rowModels, sortedRowModel: createSortedRowModel(sortFns) };
            options.initialState = { sorting: [{ desc: true, id: 'start_time' }] };
            options.manualSorting = false;
            return options;
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
