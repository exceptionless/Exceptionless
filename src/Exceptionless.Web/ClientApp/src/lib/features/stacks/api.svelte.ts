import type { WebSocketMessageValue } from '$features/websockets/models';

import { accessToken } from '$features/auth/index.svelte';
import { type FetchClientResponse, type ProblemDetails, useFetchClient } from '@exceptionless/fetchclient';
import { createMutation, createQuery, QueryClient, useQueryClient } from '@tanstack/svelte-query';

import type { Stack, StackStatus } from './models';

//
export async function invalidateStackQueries(queryClient: QueryClient, message: WebSocketMessageValue<'StackChanged'>) {
    const { id } = message;
    if (id) {
        await queryClient.invalidateQueries({ queryKey: queryKeys.id(id) });
    } else {
        await queryClient.invalidateQueries({ queryKey: queryKeys.type });
    }
}

export const queryKeys = {
    id: (id: string | undefined) => [...queryKeys.type, id] as const,
    type: ['Stack'] as const
};

export interface DeleteStackRequest {
    route: {
        ids: string | undefined;
    };
}

export interface GetStackRequest {
    route: {
        id: string | undefined;
    };
}

export interface PostAddLinkRequest {
    route: {
        id: string | undefined;
    };
}

export interface PostChangeStatusRequest {
    route: {
        ids: string | undefined;
    };
}

export interface PostMarkCriticalRequest {
    route: {
        ids: string | undefined;
    };
}

export interface PostMarkFixedRequest {
    route: {
        ids: string | undefined;
    };
}

export interface PostMarkSnoozedRequest {
    route: {
        id: string | undefined;
    };
}

export interface PostPromoteRequest {
    route: {
        ids: string | undefined;
    };
}

export interface PostRemoveLinkRequest {
    route: {
        id: string | undefined;
    };
}

export function deleteMarkCritical(request: PostMarkCriticalRequest) {
    const queryClient = useQueryClient();
    return createMutation<void, ProblemDetails, void>(() => ({
        enabled: () => !!accessToken.value && !!request.route.ids,
        mutationFn: async () => {
            const client = useFetchClient();
            await client.delete(`stacks/${request.route.ids}/mark-critical`);
        },
        mutationKey: queryKeys.id(request.route.ids),
        onError: () => {
            queryClient.invalidateQueries({ queryKey: queryKeys.id(request.route.ids) });
        },
        onSuccess: () => {
            queryClient.invalidateQueries({ queryKey: queryKeys.id(request.route.ids) });
        }
    }));
}

export function deleteStack(request: DeleteStackRequest) {
    const queryClient = useQueryClient();
    return createMutation<void, ProblemDetails, void>(() => ({
        enabled: () => !!accessToken.value && !!request.route.ids,
        mutationFn: async () => {
            const client = useFetchClient();
            await client.delete(`stacks/${request.route.ids}`);
        },
        mutationKey: queryKeys.id(request.route.ids),
        onError: () => {
            queryClient.invalidateQueries({ queryKey: queryKeys.id(request.route.ids) });
        },
        onSuccess: () => {
            queryClient.invalidateQueries({ queryKey: queryKeys.id(request.route.ids) });
        }
    }));
}
export function getStackQuery(request: GetStackRequest) {
    return createQuery<Stack, ProblemDetails>(() => ({
        enabled: () => !!accessToken.value && !!request.route.id,
        queryFn: async ({ signal }: { signal: AbortSignal }) => {
            const client = useFetchClient();
            const response = await client.getJSON<Stack>(`stacks/${request.route.id}`, {
                signal
            });

            return response.data!;
        },
        queryKey: queryKeys.id(request.route.id)
    }));
}

export function postAddLink(request: PostAddLinkRequest) {
    const queryClient = useQueryClient();
    return createMutation<void, ProblemDetails, string>(() => ({
        enabled: () => !!accessToken.value && !!request.route.id,
        mutationFn: async (url: string) => {
            const client = useFetchClient();
            await client.post(`stacks/${request.route.id}/add-link`, { value: url });
        },
        mutationKey: queryKeys.id(request.route.id),
        onError: () => {
            queryClient.invalidateQueries({ queryKey: queryKeys.id(request.route.id) });
        },
        onSuccess: () => {
            queryClient.invalidateQueries({ queryKey: queryKeys.id(request.route.id) });
        }
    }));
}

export function postChangeStatus(request: PostChangeStatusRequest) {
    const queryClient = useQueryClient();
    return createMutation<void, ProblemDetails, StackStatus>(() => ({
        enabled: () => !!accessToken.value && !!request.route.ids,
        mutationFn: async (status: StackStatus) => {
            const client = useFetchClient();
            await client.post(`stacks/${request.route.ids}/change-status`, undefined, { params: { status } });
        },
        mutationKey: queryKeys.id(request.route.ids),
        onError: () => {
            queryClient.invalidateQueries({ queryKey: queryKeys.id(request.route.ids) });
        },
        onSuccess: () => {
            queryClient.invalidateQueries({ queryKey: queryKeys.id(request.route.ids) });
        }
    }));
}

export function postMarkCritical(request: PostMarkCriticalRequest) {
    const queryClient = useQueryClient();
    return createMutation<void, ProblemDetails, void>(() => ({
        enabled: () => !!accessToken.value && !!request.route.ids,
        mutationFn: async () => {
            const client = useFetchClient();
            await client.post(`stacks/${request.route.ids}/mark-critical`);
        },
        mutationKey: queryKeys.id(request.route.ids),
        onError: () => {
            queryClient.invalidateQueries({ queryKey: queryKeys.id(request.route.ids) });
        },
        onSuccess: () => {
            queryClient.invalidateQueries({ queryKey: queryKeys.id(request.route.ids) });
        }
    }));
}

export function postMarkFixed(request: PostMarkFixedRequest) {
    const queryClient = useQueryClient();
    return createMutation<void, ProblemDetails, string | undefined>(() => ({
        enabled: () => !!accessToken.value && !!request.route.ids,
        mutationFn: async (version?: string) => {
            const client = useFetchClient();
            await client.post(`stacks/${request.route.ids}/mark-fixed`, undefined, { params: { version } });
        },
        mutationKey: queryKeys.id(request.route.ids),
        onError: () => {
            queryClient.invalidateQueries({ queryKey: queryKeys.id(request.route.ids) });
        },
        onSuccess: () => {
            queryClient.invalidateQueries({ queryKey: queryKeys.id(request.route.ids) });
        }
    }));
}

export function postMarkSnoozed(request: PostMarkSnoozedRequest) {
    const queryClient = useQueryClient();
    return createMutation<void, ProblemDetails, Date>(() => ({
        enabled: () => !!accessToken.value && !!request.route.id,
        mutationFn: async (snoozeUntilUtc: Date) => {
            const client = useFetchClient();
            await client.post(`stacks/${request.route.id}/mark-snoozed`, undefined, { params: { snoozeUntilUtc: snoozeUntilUtc.toISOString() } });
        },
        mutationKey: queryKeys.id(request.route.id),
        onError: () => {
            queryClient.invalidateQueries({ queryKey: queryKeys.id(request.route.id) });
        },
        onSuccess: () => {
            queryClient.invalidateQueries({ queryKey: queryKeys.id(request.route.id) });
        }
    }));
}

export function postPromote(request: PostPromoteRequest) {
    const queryClient = useQueryClient();
    return createMutation<FetchClientResponse<unknown>, ProblemDetails, void>(() => ({
        enabled: () => !!accessToken.value && !!request.route.ids,
        mutationFn: async () => {
            const client = useFetchClient();
            const response = await client.post(`stacks/${request.route.ids}/promote`, undefined, {
                expectedStatusCodes: [200, 404, 426, 501]
            });

            return response;
        },
        mutationKey: queryKeys.id(request.route.ids),
        onError: () => {
            queryClient.invalidateQueries({ queryKey: queryKeys.id(request.route.ids) });
        },
        onSuccess: () => {
            queryClient.invalidateQueries({ queryKey: queryKeys.id(request.route.ids) });
        }
    }));
}

export function postRemoveLink(request: PostRemoveLinkRequest) {
    const queryClient = useQueryClient();
    return createMutation<void, ProblemDetails, string>(() => ({
        enabled: () => !!accessToken.value && !!request.route.id,
        mutationFn: async (url: string) => {
            const client = useFetchClient();
            await client.post(`stacks/${request.route.id}/remove-link`, { value: url });
        },
        mutationKey: queryKeys.id(request.route.id),
        onError: () => {
            queryClient.invalidateQueries({ queryKey: queryKeys.id(request.route.id) });
        },
        onSuccess: () => {
            queryClient.invalidateQueries({ queryKey: queryKeys.id(request.route.id) });
        }
    }));
}

export async function prefetchStack(request: GetStackRequest) {
    if (!accessToken.value) {
        return;
    }

    const queryClient = useQueryClient();
    await queryClient.prefetchQuery<Stack, ProblemDetails>({
        queryFn: async ({ signal }: { signal: AbortSignal }) => {
            const client = useFetchClient();
            const response = await client.getJSON<Stack>(`stacks/${request.route.id}`, {
                signal
            });

            return response.data!;
        },
        queryKey: queryKeys.id(request.route.id)
    });
}
