import type { WebSocketMessageValue } from '$features/websockets/models';
import type { CountResult, WorkInProgressResult } from '$shared/models';

import { accessToken } from '$features/auth/index.svelte';
import { DEFAULT_OFFSET } from '$features/shared/api/api.svelte';
import { type ProblemDetails, useFetchClient } from '@exceptionless/fetchclient';
import { createMutation, createQuery, QueryClient, useQueryClient } from '@tanstack/svelte-query';

import type { PersistentEvent } from './models';

export async function invalidatePersistentEventQueries(queryClient: QueryClient, message: WebSocketMessageValue<'PersistentEventChanged'>) {
    const { id, project_id, stack_id } = message;
    if (id) {
        await queryClient.invalidateQueries({ queryKey: queryKeys.id(id) });
    }

    if (stack_id) {
        await queryClient.invalidateQueries({ queryKey: queryKeys.stacks(stack_id) });
    }

    if (project_id) {
        await queryClient.invalidateQueries({ queryKey: queryKeys.projectsCount(project_id) });
    }

    if (!id && !stack_id) {
        await queryClient.invalidateQueries({ queryKey: queryKeys.type });
    } else {
        await queryClient.invalidateQueries({ queryKey: queryKeys.count() });
    }
}

export const queryKeys = {
    count: () => [...queryKeys.type, 'count'] as const,
    deleteEvent: (ids: string[] | undefined) => [...queryKeys.type, 'delete', ...(ids ?? [])] as const,
    id: (id: string | undefined) => [...queryKeys.type, id] as const,
    projectsCount: (id: string | undefined) => [...queryKeys.type, 'projects', id] as const,
    stacks: (id: string | undefined) => [...queryKeys.type, 'stacks', id] as const,
    stacksCount: (id: string | undefined) => [...queryKeys.stacks(id), 'count'] as const,
    type: ['PersistentEvent'] as const
};

export interface DeleteEventsRequest {
    route: {
        ids: string[] | undefined;
    };
}

export interface GetCountRequest {
    params?: {
        aggregations?: string;
        filter?: string;
        mode?: 'stack_new';
        offset?: string;
        time?: string;
    };
}

export interface GetEventRequest {
    params?: {
        offset?: string;
        time?: string;
    };
    route: {
        id: string | undefined;
    };
}

export type GetEventsMode = 'stack_frequent' | 'stack_new' | 'stack_recent' | 'stack_users' | 'summary' | null;

export interface GetEventsParams {
    after?: string;
    before?: string;
    filter?: string;
    limit?: number;
    mode?: GetEventsMode;
    offset?: string;
    sort?: string;
    time?: string;
}

export interface GetProjectCountRequest {
    params?: {
        aggregations?: string;
        filter?: string;
        mode?: 'stack_new';
        offset?: string;
        time?: string;
    };
    route: {
        projectId: string | undefined;
    };
}

export interface GetStackCountRequest {
    params?: {
        aggregations?: string;
        filter?: string;
        mode?: 'stack_new';
        offset?: string;
        time?: string;
    };
    route: {
        stackId: string | undefined;
    };
}

export interface GetStackEventsRequest {
    params?: {
        after?: string;
        before?: string;
        filter?: string;
        limit?: number;
        mode?: GetEventsMode;
        offset?: string;
        sort?: string;
        time?: string;
    };
    route: {
        stackId: string | undefined;
    };
}

export function deleteEvent(request: DeleteEventsRequest) {
    const queryClient = useQueryClient();
    return createMutation<WorkInProgressResult, ProblemDetails, void>(() => ({
        enabled: () => !!accessToken.value && !!request.route.ids?.length,
        mutationFn: async () => {
            const client = useFetchClient();
            const response = await client.delete(`events/${request.route.ids?.join(',')}`);

            return response.data as WorkInProgressResult;
        },
        mutationKey: queryKeys.deleteEvent(request.route.ids),
        onError: () => {
            request.route.ids?.forEach((id) => queryClient.invalidateQueries({ queryKey: queryKeys.id(id) }));
        },
        onSuccess: () => {
            request.route.ids?.forEach((id) => queryClient.invalidateQueries({ queryKey: queryKeys.id(id) }));
        }
    }));
}

export function getCountQuery(request: GetCountRequest) {
    const queryClient = useQueryClient();

    return createQuery<CountResult, ProblemDetails>(() => ({
        enabled: () => !!accessToken.value,
        queryClient,
        queryFn: async ({ signal }: { signal: AbortSignal }) => {
            const client = useFetchClient();
            const response = await client.getJSON<CountResult>('events/count', {
                params: {
                    ...(DEFAULT_OFFSET ? { offset: DEFAULT_OFFSET } : {}),
                    ...request.params
                },
                signal
            });

            return response.data!;
        },
        queryKey: queryKeys.count()
    }));
}

export function getEventQuery(request: GetEventRequest) {
    return createQuery<PersistentEvent, ProblemDetails>(() => ({
        enabled: () => !!accessToken.value && !!request.route.id,
        queryFn: async ({ signal }: { signal: AbortSignal }) => {
            const client = useFetchClient();
            const response = await client.getJSON<PersistentEvent>(`events/${request.route.id}`, {
                params: {
                    ...(DEFAULT_OFFSET ? { offset: DEFAULT_OFFSET } : {}),
                    ...request.params
                },
                signal
            });

            return response.data!;
        },
        queryKey: queryKeys.id(request.route.id)
    }));
}

export function getProjectCountQuery(request: GetProjectCountRequest) {
    const queryClient = useQueryClient();

    return createQuery<CountResult, ProblemDetails>(() => ({
        enabled: () => !!accessToken.value && !!request.route.projectId,
        queryClient,
        queryFn: async ({ signal }: { signal: AbortSignal }) => {
            const client = useFetchClient();
            const response = await client.getJSON<CountResult>(`/projects/${request.route.projectId}/events/count`, {
                params: {
                    ...(DEFAULT_OFFSET ? { offset: DEFAULT_OFFSET } : {}),
                    ...request.params
                },
                signal
            });

            return response.data!;
        },
        queryKey: queryKeys.projectsCount(request.route.projectId)
    }));
}

export function getStackCountQuery(request: GetStackCountRequest) {
    const queryClient = useQueryClient();

    return createQuery<CountResult, ProblemDetails>(() => ({
        enabled: () => !!accessToken.value && !!request.route.stackId,
        queryClient,
        queryFn: async ({ signal }: { signal: AbortSignal }) => {
            const client = useFetchClient();
            const response = await client.getJSON<CountResult>('events/count', {
                params: {
                    ...(DEFAULT_OFFSET ? { offset: DEFAULT_OFFSET } : {}),
                    ...request.params,
                    filter: request.params?.filter?.includes(`stack:${request.route.stackId}`)
                        ? request.params.filter
                        : [request.params?.filter, `stack:${request.route.stackId}`].filter(Boolean).join(' ')
                },
                signal
            });

            return response.data!;
        },
        queryKey: queryKeys.stacksCount(request.route.stackId)
    }));
}

export function getStackEventsQuery(request: GetStackEventsRequest) {
    const queryClient = useQueryClient();

    return createQuery<PersistentEvent[], ProblemDetails>(() => ({
        enabled: () => !!accessToken.value && !!request.route.stackId,
        onSuccess: (data: PersistentEvent[]) => {
            data.forEach((event) => {
                queryClient.setQueryData(queryKeys.id(event.id!), event);
            });
        },
        queryClient,
        queryFn: async ({ signal }: { signal: AbortSignal }) => {
            const client = useFetchClient();
            const response = await client.getJSON<PersistentEvent[]>(`stacks/${request.route.stackId}/events`, {
                params: {
                    ...(DEFAULT_OFFSET ? { offset: DEFAULT_OFFSET } : {}),
                    ...request.params
                },
                signal
            });

            return response.data!;
        },
        queryKey: queryKeys.stacks(request.route.stackId)
    }));
}
