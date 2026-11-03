import type { QueryClient } from '@tanstack/svelte-query';

import { type ProblemDetails, useFetchClient } from '@foundatiofx/fetchclient';
import { createMutation, createQuery, useQueryClient } from '@tanstack/svelte-query';

import type { BulkOperationUpdated } from '#features/websockets/models.ts';

import { accessToken } from '#features/auth/index.svelte.ts';
import { queryKeys as eventQueryKeys } from '#features/events/api.svelte.ts';
import { queryKeys as stackQueryKeys } from '#features/stacks/api.svelte.ts';

import { applyBulkOperationUpdate, isBulkOperationActive, type NewBulkOperation, type ViewBulkOperation } from './models';

/** While an operation is active the list is polled as well, so progress still moves when push messages are unavailable. */
export const ACTIVE_OPERATION_POLL_INTERVAL_MS = 3000;

export const queryKeys = {
    cancel: (id: string | undefined) => [...queryKeys.type, id, 'cancel'] as const,
    organization: (organizationId: string | undefined) => [...queryKeys.type, 'organization', organizationId] as const,
    post: () => [...queryKeys.type, 'post'] as const,
    type: ['BulkOperation'] as const
};

export interface GetBulkOperationsRequest {
    route: {
        organizationId: string | undefined;
    };
}

export function cancelBulkOperation() {
    const queryClient = useQueryClient();

    return createMutation<ViewBulkOperation, ProblemDetails, string>(() => ({
        enabled: () => !!accessToken.current,
        mutationFn: async (id: string) => {
            const client = useFetchClient();
            const response = await client.postJSON<ViewBulkOperation>(`bulk-operations/${id}/cancel`);

            return response.data!;
        },
        mutationKey: queryKeys.cancel(undefined),
        onSuccess: (operation) => {
            upsertBulkOperation(queryClient, operation);
        }
    }));
}

export function getBulkOperationsQuery(request: GetBulkOperationsRequest) {
    return createQuery<ViewBulkOperation[], ProblemDetails>(() => ({
        enabled: () => !!accessToken.current && !!request.route.organizationId,
        queryFn: async ({ signal }: { signal: AbortSignal }) => {
            const client = useFetchClient();
            const response = await client.getJSON<ViewBulkOperation[]>(`organizations/${request.route.organizationId}/bulk-operations`, {
                signal
            });

            return response.data ?? [];
        },
        queryKey: queryKeys.organization(request.route.organizationId),
        refetchInterval: (query) => (query.state.data?.some((operation) => isBulkOperationActive(operation.status)) ? ACTIVE_OPERATION_POLL_INTERVAL_MS : false)
    }));
}

/** Refreshes everything a finished bulk operation may have changed. */
export async function invalidateBulkOperationAffectedQueries(queryClient: QueryClient) {
    await Promise.all([
        queryClient.invalidateQueries({
            queryKey: eventQueryKeys.type
        }),
        queryClient.invalidateQueries({
            queryKey: stackQueryKeys.type
        })
    ]);
}

export function postBulkOperation() {
    const queryClient = useQueryClient();

    return createMutation<ViewBulkOperation, ProblemDetails, NewBulkOperation>(() => ({
        enabled: () => !!accessToken.current,
        mutationFn: async (operation: NewBulkOperation) => {
            const client = useFetchClient();
            const response = await client.postJSON<ViewBulkOperation>('bulk-operations', operation);

            return response.data!;
        },
        mutationKey: queryKeys.post(),
        onSuccess: (operation) => {
            upsertBulkOperation(queryClient, operation);
        }
    }));
}

/** Applies a pushed update to the cached list. Unknown operations (started elsewhere) trigger a refetch instead. */
export function updateBulkOperationFromMessage(queryClient: QueryClient, update: BulkOperationUpdated) {
    const key = queryKeys.organization(update.organization_id);
    const current = queryClient.getQueryData<ViewBulkOperation[]>(key);
    const next = current ? applyBulkOperationUpdate(current, update) : undefined;
    if (!next) {
        void queryClient.invalidateQueries({
            queryKey: key
        });
        return;
    }

    queryClient.setQueryData(key, next);
}

function upsertBulkOperation(queryClient: QueryClient, operation: ViewBulkOperation) {
    queryClient.setQueryData<ViewBulkOperation[]>(queryKeys.organization(operation.organization_id), (current) => [
        operation,
        ...(current ?? []).filter((existing) => existing.id !== operation.id)
    ]);
}
