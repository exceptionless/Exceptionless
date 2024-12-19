import type { WebSocketMessageValue } from '$features/websockets/models';

import { accessToken } from '$features/auth/index.svelte';
import { type ProblemDetails, useFetchClient } from '@exceptionless/fetchclient';
import { createQuery, QueryClient, useQueryClient } from '@tanstack/svelte-query';

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
        await queryClient.invalidateQueries({ queryKey: queryKeys.projects(project_id) });
    }

    if (!id && !stack_id) {
        await queryClient.invalidateQueries({ queryKey: queryKeys.type });
    }
}

export const queryKeys = {
    id: (id: string | undefined) => [...queryKeys.type, id] as const,
    projects: (id: string | undefined) => [...queryKeys.type, 'projects', id] as const,
    stacks: (id: string | undefined) => [...queryKeys.type, 'stacks', id] as const,
    type: ['PersistentEvent'] as const
};

export interface GetEventRequest {
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

export function getEventQuery(request: GetEventRequest) {
    return createQuery<PersistentEvent, ProblemDetails>(() => ({
        enabled: () => !!accessToken.value && !!request.route.id,
        queryFn: async ({ signal }: { signal: AbortSignal }) => {
            const client = useFetchClient();
            const response = await client.getJSON<PersistentEvent>(`events/${request.route.id}`, {
                signal
            });

            return response.data!;
        },
        queryKey: queryKeys.id(request.route.id)
    }));
}

export function getProjectCountQuery(request: GetProjectCountRequest) {
    const queryClient = useQueryClient();

    return createQuery<PersistentEvent[], ProblemDetails>(() => ({
        enabled: () => !!accessToken.value && !!request.route.projectId,
        queryClient,
        queryFn: async ({ signal }: { signal: AbortSignal }) => {
            const client = useFetchClient();
            const response = await client.getJSON<PersistentEvent[]>(`/projects/${request.route.projectId}/events/count`, {
                params: request.params,
                signal
            });

            return response.data!;
        },
        queryKey: queryKeys.projects(request.route.projectId)
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
                params: request.params,
                signal
            });

            return response.data!;
        },
        queryKey: queryKeys.stacks(request.route.stackId)
    }));
}
