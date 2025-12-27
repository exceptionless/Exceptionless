import type { Webhook } from '$features/webhooks/models';
import type { FetchClientResponse, ProblemDetails } from '@exceptionless/fetchclient';
import type { CreateQueryResult } from '@tanstack/svelte-query';

import { getSharedTableOptions } from '$features/shared/table.svelte';
import WebhookActionsCell from '$features/webhooks/components/table/webhook-actions-cell.svelte';
import { type ColumnDef, renderComponent } from '@tanstack/svelte-table';

import type { GetProjectWebhooksParams } from '../../api.svelte';

export function getColumns<TWebhook extends Webhook>(): ColumnDef<TWebhook>[] {
    const columns: ColumnDef<TWebhook>[] = [
        {
            accessorKey: 'url',
            cell: (info) => info.getValue(),
            enableHiding: false,
            enableSorting: false,
            header: 'Url',
            meta: {
                class: 'w-[200px]'
            }
        },
        {
            accessorKey: 'event_types',
            cell: (info) => info.getValue(),
            enableHiding: true,
            enableSorting: false,
            header: 'Event Types',
            meta: {
                class: 'w-[200px]'
            }
        },
        {
            cell: (info) => renderComponent(WebhookActionsCell, { webhook: info.row.original }),
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

export function getTableOptions<TWebhook extends Webhook>(
    queryParameters: GetProjectWebhooksParams,
    queryResponse: CreateQueryResult<FetchClientResponse<TWebhook[]>, ProblemDetails>
) {
    return getSharedTableOptions<TWebhook>({
        columnPersistenceKey: 'webhooks-column-visibility',
        get columns() {
            return getColumns<TWebhook>();
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
