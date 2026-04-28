import type { ElasticsearchIndexDetail } from '$features/admin/models';

import Bytes from '$comp/formatters/bytes.svelte';
import Number from '$comp/formatters/number.svelte';
import { getSharedTableOptions, type TableMemoryPagingParameters } from '$features/shared/table.svelte';
import { type ColumnDef, createSortedRowModel, renderComponent, sortFns, type StockFeatures } from '@tanstack/svelte-table';

import HealthBadgeCell from './health-badge-cell.svelte';
import UnassignedShardsCell from './unassigned-shards-cell.svelte';

export function getColumns(): ColumnDef<StockFeatures, ElasticsearchIndexDetail, unknown>[] {
    return [
        {
            accessorKey: 'index',
            cell: (info) => info.getValue(),
            enableSorting: true,
            header: 'Index',
            meta: {
                class: 'max-w-xs'
            }
        },
        {
            accessorKey: 'health',
            cell: (info) => renderComponent(HealthBadgeCell, { value: info.getValue() as null | string | undefined }),
            enableSorting: true,
            header: 'Health'
        },
        {
            accessorKey: 'status',
            cell: (info) => info.getValue(),
            enableSorting: true,
            header: 'Status'
        },
        {
            accessorKey: 'primary',
            cell: (info) => renderComponent(Number, { value: info.getValue() as null | number }),
            enableSorting: true,
            header: 'Primary',
            meta: {
                class: 'text-right'
            }
        },
        {
            accessorKey: 'replica',
            cell: (info) => renderComponent(Number, { value: info.getValue() as null | number }),
            enableSorting: true,
            header: 'Replica',
            meta: {
                class: 'text-right'
            }
        },
        {
            accessorKey: 'unassigned_shards',
            cell: (info) => renderComponent(UnassignedShardsCell, { value: info.getValue() as number }),
            enableSorting: true,
            header: 'Unassigned',
            meta: {
                class: 'text-right'
            }
        },
        {
            accessorKey: 'docs_count',
            cell: (info) => renderComponent(Number, { value: info.getValue() as null | number }),
            enableSorting: true,
            header: 'Documents',
            meta: {
                class: 'text-right'
            }
        },
        {
            accessorKey: 'store_size_in_bytes',
            cell: (info) => renderComponent(Bytes, { value: info.getValue() as null | number }),
            enableSorting: true,
            header: 'Size',
            meta: {
                class: 'text-right'
            }
        }
    ];
}

export function getTableOptions(queryParameters: TableMemoryPagingParameters, getData: () => ElasticsearchIndexDetail[]) {
    return getSharedTableOptions<ElasticsearchIndexDetail, 'memory'>({
        columnPersistenceKey: 'admin-indices',
        get columns() {
            return getColumns();
        },
        configureOptions: (options) => {
            options._rowModels = { ...options._rowModels, sortedRowModel: createSortedRowModel(sortFns) };
            options.initialState = { sorting: [{ desc: true, id: 'store_size_in_bytes' }] };
            options.manualSorting = false;
            return options;
        },
        defaultColumnVisibility: {
            primary: false,
            replica: false
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
