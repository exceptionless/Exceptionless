import type { NewWebhook, Webhook } from '$features/webhooks/models';
import type { WebSocketMessageValue } from '$features/websockets/models';

import { accessToken } from '$features/auth/index.svelte';
import { DEFAULT_LIMIT } from '$features/shared/api/api.svelte';
import { type FetchClientResponse, type ProblemDetails, useFetchClient } from '@exceptionless/fetchclient';
import { createMutation, createQuery, QueryClient, useQueryClient } from '@tanstack/svelte-query';

export async function invalidateWebhookQueries(queryClient: QueryClient, message: WebSocketMessageValue<'WebhookChanged'>) {
    const { id, organization_id, project_id } = message;
    if (id) {
        await queryClient.invalidateQueries({ queryKey: queryKeys.id(id) });
    }

    //     if (organization_id) {
    //         await queryClient.invalidateQueries({ queryKey: queryKeys.organization(organization_id) });
    //     }

    if (project_id) {
        await queryClient.invalidateQueries({ queryKey: queryKeys.project(project_id) });
    }

    if (!id && !organization_id && !project_id) {
        await queryClient.invalidateQueries({ queryKey: queryKeys.type });
    }
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
            request.route.ids?.forEach((id) => queryClient.invalidateQueries({ queryKey: queryKeys.id(id) }));
        },
        onSuccess: () => {
            request.route.ids?.forEach((id) => queryClient.invalidateQueries({ queryKey: queryKeys.id(id) }));
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
            queryClient.invalidateQueries({ queryKey: queryKeys.type });
            queryClient.setQueryData(queryKeys.id(webhook.id), webhook);
        }
    }));
}
