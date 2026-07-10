import type { NewToken, UpdateToken, ViewToken } from '$features/tokens/models';
import type { WebSocketMessageValue } from '$features/websockets/models';

import { accessToken } from '$features/auth/index.svelte';
import { DEFAULT_LIMIT } from '$features/shared/api/api.svelte';
import { type FetchClientResponse, type ProblemDetails, useFetchClient } from '@exceptionless/fetchclient';
import { createMutation, createQuery, QueryClient, useQueryClient } from '@tanstack/svelte-query';

export async function invalidateTokenQueries(queryClient: QueryClient, message: WebSocketMessageValue<'TokenChanged'>) {
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
    deleteToken: (ids: string[] | undefined) => [...queryKeys.ids(ids), 'delete'] as const,
    id: (id: string | undefined) => [...queryKeys.type, id] as const,
    ids: (ids: string[] | undefined) => [...queryKeys.type, ...(ids ?? [])] as const,
    postProjectToken: (id: string | undefined) => [...queryKeys.project(id), 'post'] as const,
    project: (id: string | undefined) => [...queryKeys.type, 'project', id] as const,
    projectDefaultToken: (projectId: string | undefined) => [...queryKeys.project(projectId), 'default'] as const,
    type: ['Token'] as const
};

export interface DeleteTokenRequest {
    route: {
        ids: string[];
    };
}

export interface GetProjectDefaultTokenRequest {
    route: {
        projectId: string | undefined;
    };
}

export interface GetProjectTokensParams {
    limit?: number;
    page?: number;
}

export interface GetProjectTokensRequest {
    params: GetProjectTokensParams;
    route: {
        projectId: string | undefined;
    };
}

export interface PatchTokenRequest {
    route: {
        id: string | undefined;
    };
}

export interface PostProjectTokenRequest {
    route: {
        projectId: string;
    };
}

export function deleteToken(request: DeleteTokenRequest) {
    const queryClient = useQueryClient();

    return createMutation<FetchClientResponse<unknown>, ProblemDetails, void>(() => ({
        enabled: () => !!accessToken.current && !!request.route.ids?.length,
        mutationFn: async () => {
            const client = useFetchClient();
            const response = await client.delete(`tokens/${request.route.ids?.join(',')}`, {
                expectedStatusCodes: [202]
            });

            return response;
        },
        mutationKey: queryKeys.deleteToken(request.route.ids),
        onError: () => {
            request.route.ids?.forEach((id) => queryClient.invalidateQueries({ queryKey: queryKeys.id(id) }));
        },
        onSuccess: () => {
            request.route.ids?.forEach((id) => queryClient.invalidateQueries({ queryKey: queryKeys.id(id) }));
        }
    }));
}

export function getProjectDefaultTokenQuery(request: GetProjectDefaultTokenRequest) {
    const queryClient = useQueryClient();

    return createQuery<ViewToken, ProblemDetails>(() => ({
        enabled: () => !!accessToken.current && !!request.route.projectId,
        onSuccess: (token: ViewToken) => {
            queryClient.setQueryData(queryKeys.id(token.id!), token);
        },
        queryClient,
        queryFn: async ({ signal }: { signal: AbortSignal }) => {
            const client = useFetchClient();
            const response = await client.getJSON<ViewToken>(`projects/${request.route.projectId}/tokens/default`, {
                signal
            });

            return response.data!;
        },
        queryKey: queryKeys.projectDefaultToken(request.route.projectId)
    }));
}

export function getProjectTokensQuery(request: GetProjectTokensRequest) {
    const queryClient = useQueryClient();

    return createQuery<FetchClientResponse<ViewToken[]>, ProblemDetails>(() => ({
        enabled: () => !!accessToken.current && !!request.route.projectId,
        onSuccess: (data: FetchClientResponse<ViewToken[]>) => {
            data.data?.forEach((token) => {
                queryClient.setQueryData(queryKeys.id(token.id!), token);
            });
        },
        queryClient,
        queryFn: async ({ signal }: { signal: AbortSignal }) => {
            const client = useFetchClient();
            const response = await client.getJSON<ViewToken[]>(`projects/${request.route.projectId}/tokens`, {
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

export function patchToken(request: PatchTokenRequest) {
    const queryClient = useQueryClient();

    return createMutation<ViewToken, ProblemDetails, UpdateToken>(() => ({
        mutationFn: async (data: UpdateToken) => {
            const client = useFetchClient();
            const response = await client.patchJSON<ViewToken>(`tokens/${request.route.id}`, data);
            return response.data!;
        },
        mutationKey: queryKeys.id(request.route.id),
        onError: () => {
            queryClient.invalidateQueries({ queryKey: queryKeys.id(request.route.id) });
        },
        onSuccess: (token: ViewToken) => {
            queryClient.setQueryData(queryKeys.id(request.route.id), token);
        }
    }));
}

export function postProjectToken(request: PostProjectTokenRequest) {
    const queryClient = useQueryClient();

    return createMutation<ViewToken, ProblemDetails, NewToken>(() => ({
        enabled: () => !!accessToken.current && request.route.projectId,
        mutationFn: async (token: NewToken) => {
            const client = useFetchClient();
            const response = await client.postJSON<ViewToken>(`projects/${request.route.projectId}/tokens`, token);
            return response.data!;
        },
        mutationKey: queryKeys.postProjectToken(request.route.projectId),
        onSuccess: (token: ViewToken) => {
            queryClient.invalidateQueries({ queryKey: queryKeys.type });
            queryClient.setQueryData(queryKeys.id(token.id), token);
        }
    }));
}
