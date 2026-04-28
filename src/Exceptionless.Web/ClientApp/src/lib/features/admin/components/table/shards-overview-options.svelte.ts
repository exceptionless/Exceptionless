import type { ShardMetric } from '$features/admin/models';

import { getSharedTableOptions, type TableMemoryPagingParameters } from '$features/shared/table.svelte';
import { type ColumnDef, renderComponent, type StockFeatures } from '@tanstack/svelte-table';

import ShardMetricCell from './shard-metric-cell.svelte';
import ShardValueCell from './shard-value-cell.svelte';

export function getColumns(): ColumnDef<StockFeatures, ShardMetric, unknown>[] {
    return [
        {
            accessorKey: 'label',
            cell: (info) =>
                renderComponent(ShardMetricCell, {
                    label: info.row.original.label,
                    metricId: info.row.original.id,
                    value: info.row.original.value
                }),
            enableSorting: false,
            header: 'Metric'
        },
        {
            accessorKey: 'value',
            cell: (info) =>
                renderComponent(ShardValueCell, {
                    metricId: info.row.original.id,
                    value: info.getValue() as number
                }),
            enableSorting: false,
            header: 'Count',
            meta: {
                class: 'text-right'
            }
        }
    ];
}

export function getTableOptions(queryParameters: TableMemoryPagingParameters, getData: () => ShardMetric[]) {
    return getSharedTableOptions<ShardMetric, 'memory'>({
        columnPersistenceKey: 'admin-shards-overview',
        get columns() {
            return getColumns();
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
