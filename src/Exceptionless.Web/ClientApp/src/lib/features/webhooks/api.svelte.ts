import type { NewWebhook, Webhook } from '$features/webhooks/models';
import type { WebSocketMessageValue } from '$features/websockets/models';

import { accessToken } from '$features/auth/index.svelte';
import { DEFAULT_LIMIT } from '$features/shared/api/api.svelte';
import { ChangeType } from '$features/websockets/models';
import { type FetchClientResponse, type ProblemDetails, useFetchClient } from '@exceptionless/fetchclient';
import { createMutation, createQuery, QueryClient, useQueryClient } from '@tanstack/svelte-query';

export const WEBHOOK_REFRESH_DELAY_MS = 1500;

export async function invalidateWebhookQueries(queryClient: QueryClient, message: WebSocketMessageValue<'WebhookChanged'>) {
    const { change_type, id, project_id } = message;

    if (change_type === ChangeType.Removed && id) {
        removeWebhooksFromCaches(queryClient, [id]);
        scheduleWebhookInvalidation(queryClient, id, project_id);
        return;
    }

    // Added/Saved events can arrive before Elasticsearch exposes the change to list queries.
    if (change_type === ChangeType.Added || change_type === ChangeType.Saved) {
        scheduleWebhookInvalidation(queryClient, id, project_id);
        return;
    }

    await invalidateWebhookCache(queryClient, id, project_id);
}

// TODO: Do we need to scope these all by organization?
export const queryKeys = {
    deleteWebhook: (ids: string[] | undefined) => [...queryKeys.ids(ids), 'delete'] as const,
    id: (id: string | undefined) => [...queryKeys.type, id] as const,
    ids: (ids: string[] | undefined) => [...queryKeys.type, ...(ids ?? [])] as const,
    postWebhook: () => [...queryKeys.type, 'post'] as const,
    project: (id: string | undefined) => [...queryKeys.type, 'project', id] as const,
    type: ['Webhook'] as const
};

export interface DeleteWebhookRequest {
    route: {
        ids: string[];
    };
}

export interface GetProjectWebhooksParams {
    limit?: number;
    page?: number;
}

export interface GetProjectWebhooksRequest {
    params: GetProjectWebhooksParams;
    route: {
        projectId: string | undefined;
    };
}

export function deleteWebhook(request: DeleteWebhookRequest) {
    const queryClient = useQueryClient();

    return createMutation<FetchClientResponse<unknown>, ProblemDetails, void>(() => ({
        enabled: () => !!accessToken.current && !!request.route.ids?.length,
        mutationFn: async () => {
            const client = useFetchClient();
            const response = await client.delete(`webhooks/${request.route.ids?.join(',')}`, {
                expectedStatusCodes: [202]
            });

            return response;
        },
        mutationKey: queryKeys.deleteWebhook(request.route.ids),
        onError: () => {
            void queryClient.invalidateQueries({ queryKey: queryKeys.type });
        },
        onMutate: () => {
            removeWebhooksFromCaches(queryClient, request.route.ids);
        },
        onSuccess: () => {
            removeWebhooksFromCaches(queryClient, request.route.ids);
            scheduleWebhookInvalidation(queryClient);
        }
    }));
}

export function getProjectWebhooksQuery(request: GetProjectWebhooksRequest) {
    const queryClient = useQueryClient();

    return createQuery<FetchClientResponse<Webhook[]>, ProblemDetails>(() => ({
        enabled: () => !!accessToken.current && !!request.route.projectId,
        onSuccess: (data: FetchClientResponse<Webhook[]>) => {
            data.data?.forEach((webhook) => {
                queryClient.setQueryData(queryKeys.id(webhook.id!), webhook);
            });
        },
        queryClient,
        queryFn: async ({ signal }: { signal: AbortSignal }) => {
            const client = useFetchClient();
            const response = await client.getJSON<Webhook[]>(`projects/${request.route.projectId}/webhooks`, {
                params: {
                    ...request.params,
                    limit: request.params?.limit ?? DEFAULT_LIMIT
                },
                signal
            });

            return response;
        },
        queryKey: [...queryKeys.project(request.route.projectId), { params: request.params }]
    }));
}

export function postWebhook() {
    const queryClient = useQueryClient();

    return createMutation<Webhook, ProblemDetails, NewWebhook>(() => ({
        enabled: () => !!accessToken.current,
        mutationFn: async (webhook: NewWebhook) => {
            const client = useFetchClient();
            const response = await client.postJSON<Webhook>('webhooks', webhook);
            return response.data!;
        },
        mutationKey: queryKeys.postWebhook(),
        onSuccess: (webhook: Webhook) => {
            syncWebhookCaches(queryClient, webhook);
            scheduleWebhookInvalidation(queryClient, webhook.id, webhook.project_id);
        }
    }));
}

export function removeWebhooksFromCaches(queryClient: QueryClient, ids: string[] | undefined) {
    if (!ids?.length) {
        return;
    }

    queryClient.setQueriesData<FetchClientResponse<Webhook[]> | undefined>({ queryKey: queryKeys.type }, (response) => {
        if (!Array.isArray(response?.data)) {
            return response;
        }

        return { ...response, data: response.data.filter((webhook) => !ids.includes(webhook.id)) };
    });

    ids.forEach((id) => queryClient.removeQueries({ queryKey: queryKeys.id(id) }));
}

export function syncWebhookCaches(queryClient: QueryClient, webhook: Webhook) {
    queryClient.setQueryData(queryKeys.id(webhook.id), webhook);
    queryClient.setQueriesData<FetchClientResponse<Webhook[]> | undefined>({ queryKey: queryKeys.project(webhook.project_id) }, (response) => {
        if (!Array.isArray(response?.data)) {
            return response;
        }

        const exists = response.data.some((existingWebhook) => existingWebhook.id === webhook.id);
        return {
            ...response,
            data: exists ? response.data.map((existingWebhook) => (existingWebhook.id === webhook.id ? webhook : existingWebhook)) : [...response.data, webhook]
        };
    });
}

async function invalidateWebhookCache(queryClient: QueryClient, id?: string, projectId?: string) {
    if (id) {
        await queryClient.invalidateQueries({ queryKey: queryKeys.id(id) });
    }

    if (projectId) {
        await queryClient.invalidateQueries({ queryKey: queryKeys.project(projectId) });
    }

    if (!id && !projectId) {
        await queryClient.invalidateQueries({ queryKey: queryKeys.type });
    }
}

function scheduleWebhookInvalidation(queryClient: QueryClient, id?: string, projectId?: string) {
    setTimeout(() => {
        void invalidateWebhookCache(queryClient, id, projectId);
    }, WEBHOOK_REFRESH_DELAY_MS);
}
