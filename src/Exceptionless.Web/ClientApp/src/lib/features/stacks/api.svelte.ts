import type { WebSocketMessageValue } from '$features/websockets/models';
import type { WorkInProgressResult } from '$shared/models';

import { accessToken } from '$features/auth/index.svelte';
import { type FetchClientResponse, type ProblemDetails, useFetchClient } from '@exceptionless/fetchclient';
import { createMutation, createQuery, QueryClient, useQueryClient } from '@tanstack/svelte-query';

import type { Stack, StackStatus } from './models';

export async function invalidateStackQueries(queryClient: QueryClient, message: WebSocketMessageValue<'StackChanged'>) {
    const { id } = message;
    if (id) {
        await queryClient.invalidateQueries({ queryKey: queryKeys.id(id) });
    } else {
        await queryClient.invalidateQueries({ queryKey: queryKeys.type });
    }
}

export const queryKeys = {
    deleteMarkCritical: (ids: string[] | undefined) => [...queryKeys.ids(ids), 'mark-not-critical'] as const,
    deleteStack: (ids: string[] | undefined) => [...queryKeys.ids(ids), 'delete'] as const,
    id: (id: string | undefined) => [...queryKeys.type, id] as const,
    ids: (ids: string[] | undefined) => [...queryKeys.type, ...(ids ?? [])] as const,
    postAddLink: (id: string | undefined) => [...queryKeys.id(id), 'add-link'] as const,
    postChangeStatus: (ids: string[] | undefined) => [...queryKeys.ids(ids), 'change-status'] as const,
    postMarkCritical: (ids: string[] | undefined) => [...queryKeys.ids(ids), 'mark-critical'] as const,
    postMarkFixed: (ids: string[] | undefined) => [...queryKeys.ids(ids), 'mark-fixed'] as const,
    postMarkSnoozed: (ids: string[] | undefined) => [...queryKeys.ids(ids), 'mark-snoozed'] as const,
    postPromote: (ids: string[] | undefined) => [...queryKeys.ids(ids), 'promote'] as const,
    postRemoveLink: (id: string | undefined) => [...queryKeys.id(id), 'remove-link'] as const,
    type: ['Stack'] as const
};

export interface DeleteStackRequest {
    route: {
        ids: string[] | undefined;
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
        ids: string[] | undefined;
    };
}

export interface PostMarkCriticalRequest {
    route: {
        ids: string[] | undefined;
    };
}

export interface PostMarkFixedRequest {
    route: {
        ids: string[] | undefined;
    };
}

export interface PostMarkSnoozedRequest {
    route: {
        ids: string[] | undefined;
    };
}

export interface PostPromoteRequest {
    route: {
        ids: string[] | undefined;
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
        enabled: () => !!accessToken.current && !!request.route.ids?.length,
        mutationFn: async () => {
            const client = useFetchClient();
            await client.delete(`stacks/${request.route.ids?.join(',')}/mark-critical`);
        },
        mutationKey: queryKeys.deleteMarkCritical(request.route.ids),
        onError: () => {
            request.route.ids?.forEach((id) => queryClient.invalidateQueries({ queryKey: queryKeys.id(id) }));
        },
        onSuccess: () => {
            request.route.ids?.forEach((id) => queryClient.invalidateQueries({ queryKey: queryKeys.id(id) }));
        }
    }));
}

export function deleteStack(request: DeleteStackRequest) {
    const queryClient = useQueryClient();
    return createMutation<WorkInProgressResult, ProblemDetails, void>(() => ({
        enabled: () => !!accessToken.current && !!request.route.ids?.length,
        mutationFn: async () => {
            const client = useFetchClient();
            const response = await client.deleteJSON<WorkInProgressResult>(`stacks/${request.route.ids?.join(',')}`);

            return response.data!;
        },
        mutationKey: queryKeys.deleteStack(request.route.ids),
        onError: () => {
            request.route.ids?.forEach((id) => queryClient.invalidateQueries({ queryKey: queryKeys.id(id) }));
        },
        onSuccess: () => {
            request.route.ids?.forEach((id) => queryClient.invalidateQueries({ queryKey: queryKeys.id(id) }));
        }
    }));
}

export function getStackQuery(request: GetStackRequest) {
    return createQuery<Stack, ProblemDetails>(() => ({
        enabled: () => !!accessToken.current && !!request.route.id,
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
        enabled: () => !!accessToken.current && !!request.route.id,
        mutationFn: async (url: string) => {
            const client = useFetchClient();
            await client.post(`stacks/${request.route.id}/add-link`, { value: url });
        },
        mutationKey: queryKeys.postAddLink(request.route.id),
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
        enabled: () => !!accessToken.current && !!request.route.ids?.length,
        mutationFn: async (status: StackStatus) => {
            const client = useFetchClient();
            await client.post(`stacks/${request.route.ids?.join(',')}/change-status`, undefined, { params: { status } });
        },
        mutationKey: queryKeys.postChangeStatus(request.route.ids),
        onError: () => {
            request.route.ids?.forEach((id) => queryClient.invalidateQueries({ queryKey: queryKeys.id(id) }));
        },
        onSuccess: () => {
            request.route.ids?.forEach((id) => queryClient.invalidateQueries({ queryKey: queryKeys.id(id) }));
        }
    }));
}

export function postMarkCritical(request: PostMarkCriticalRequest) {
    const queryClient = useQueryClient();
    return createMutation<void, ProblemDetails, void>(() => ({
        enabled: () => !!accessToken.current && !!request.route.ids?.length,
        mutationFn: async () => {
            const client = useFetchClient();
            await client.post(`stacks/${request.route.ids?.join(',')}/mark-critical`);
        },
        mutationKey: queryKeys.postMarkCritical(request.route.ids),
        onError: () => {
            request.route.ids?.forEach((id) => queryClient.invalidateQueries({ queryKey: queryKeys.id(id) }));
        },
        onSuccess: () => {
            request.route.ids?.forEach((id) => queryClient.invalidateQueries({ queryKey: queryKeys.id(id) }));
        }
    }));
}

export function postMarkFixed(request: PostMarkFixedRequest) {
    const queryClient = useQueryClient();
    return createMutation<void, ProblemDetails, string | undefined>(() => ({
        enabled: () => !!accessToken.current && !!request.route.ids?.length,
        mutationFn: async (version?: string) => {
            const client = useFetchClient();
            await client.post(`stacks/${request.route.ids?.join(',')}/mark-fixed`, undefined, { params: { version } });
        },
        mutationKey: queryKeys.postMarkFixed(request.route.ids),
        onError: () => {
            request.route.ids?.forEach((id) => queryClient.invalidateQueries({ queryKey: queryKeys.id(id) }));
        },
        onSuccess: () => {
            request.route.ids?.forEach((id) => queryClient.invalidateQueries({ queryKey: queryKeys.id(id) }));
        }
    }));
}

export function postMarkSnoozed(request: PostMarkSnoozedRequest) {
    const queryClient = useQueryClient();
    return createMutation<void, ProblemDetails, Date>(() => ({
        enabled: () => !!accessToken.current && !!request.route.ids?.length,
        mutationFn: async (snoozeUntilUtc: Date) => {
            const client = useFetchClient();
            await client.post(`stacks/${request.route.ids?.join(',')}/mark-snoozed`, undefined, { params: { snoozeUntilUtc: snoozeUntilUtc.toISOString() } });
        },
        mutationKey: queryKeys.postMarkSnoozed(request.route.ids),
        onError: () => {
            request.route.ids?.forEach((id) => queryClient.invalidateQueries({ queryKey: queryKeys.id(id) }));
        },
        onSuccess: () => {
            request.route.ids?.forEach((id) => queryClient.invalidateQueries({ queryKey: queryKeys.id(id) }));
        }
    }));
}

export function postPromote(request: PostPromoteRequest) {
    const queryClient = useQueryClient();
    return createMutation<FetchClientResponse<unknown>, ProblemDetails, void>(() => ({
        enabled: () => !!accessToken.current && !!request.route.ids?.length,
        mutationFn: async () => {
            const client = useFetchClient();
            const response = await client.post(`stacks/${request.route.ids?.join(',')}/promote`, undefined, {
                expectedStatusCodes: [200, 404, 426, 501]
            });

            return response;
        },
        mutationKey: queryKeys.postPromote(request.route.ids),
        onError: () => {
            request.route.ids?.forEach((id) => queryClient.invalidateQueries({ queryKey: queryKeys.id(id) }));
        },
        onSuccess: () => {
            request.route.ids?.forEach((id) => queryClient.invalidateQueries({ queryKey: queryKeys.id(id) }));
        }
    }));
}

export function postRemoveLink(request: PostRemoveLinkRequest) {
    const queryClient = useQueryClient();
    return createMutation<void, ProblemDetails, string>(() => ({
        enabled: () => !!accessToken.current && !!request.route.id,
        mutationFn: async (url: string) => {
            const client = useFetchClient();
            await client.post(`stacks/${request.route.id}/remove-link`, { value: url });
        },
        mutationKey: queryKeys.postRemoveLink(request.route.id),
        onError: () => {
            queryClient.invalidateQueries({ queryKey: queryKeys.id(request.route.id) });
        },
        onSuccess: () => {
            queryClient.invalidateQueries({ queryKey: queryKeys.id(request.route.id) });
        }
    }));
}

export async function prefetchStack(request: GetStackRequest) {
    if (!accessToken.current) {
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
