import type { FetchClientResponse, ProblemDetails } from '@exceptionless/fetchclient';
import type { CreateQueryResult } from '@tanstack/svelte-query';

import { getSharedTableOptions } from '$features/shared/table.svelte';
import TokenActionsCell from '$features/tokens/components/table/token-actions-cell.svelte';
import TokenIdCell from '$features/tokens/components/table/token-id-cell.svelte';
import { ViewToken } from '$features/tokens/models';
import { type ColumnDef, renderComponent } from '@tanstack/svelte-table';

import type { GetProjectTokensParams } from '../../api.svelte';

export function getColumns<TToken extends ViewToken>(): ColumnDef<TToken>[] {
    const columns: ColumnDef<TToken>[] = [
        {
            accessorKey: 'id',
            cell: (info) => renderComponent(TokenIdCell, { token: info.row.original }),
            enableHiding: false,
            enableSorting: false,
            header: 'API Key',
            meta: {
                class: 'w-[180px]'
            }
        },
        {
            accessorKey: 'notes',
            cell: (info) => info.getValue(),
            enableHiding: true,
            enableSorting: false,
            header: 'Notes',
            meta: {
                class: 'w-[200px]'
            }
        },
        {
            cell: (info) => renderComponent(TokenActionsCell, { token: info.row.original }),
            enableHiding: false,
            enableSorting: false,
            header: 'Actions',
            id: 'actions',
            meta: {
                class: 'w-16'
            }
        }
    ];

    return columns;
}

export function getTableOptions<TToken extends ViewToken>(
    queryParameters: GetProjectTokensParams,
    queryResponse: CreateQueryResult<FetchClientResponse<TToken[]>, ProblemDetails>
) {
    return getSharedTableOptions<TToken>({
        columnPersistenceKey: 'tokens-column-visibility',
        get columns() {
            return getColumns<TToken>();
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
