import { accessToken } from '$features/auth/index.svelte';
import { type ProblemDetails, useFetchClient } from '@exceptionless/fetchclient';
import { createQuery, useQueryClient } from '@tanstack/svelte-query';

import type { PersistentEvent } from './models';

export const queryKeys = {
    all: ['PersistentEvent'] as const,
    allWithFilters: (filters: string) => [...queryKeys.all, { filters }] as const,
    id: (id: string | undefined) => [...queryKeys.all, id] as const,
    stacks: (id: string | undefined) => [...queryKeys.all, 'stacks', id] as const,
    stackWithFilters: (id: string | undefined, filters: string) => [...queryKeys.stacks(id), { filters }] as const
};

export interface GetEventByIdProps {
    id: string | undefined;
}

export type GetEventsMode = 'stack_frequent' | 'stack_new' | 'stack_recent' | 'stack_users' | 'summary' | null;

export interface IGetEventsParams {
    after?: string;
    before?: string;
    filter?: string;
    limit?: number;
    mode?: GetEventsMode;
    offset?: string;
    page?: number;
    sort?: string;
    time?: string;
}

export function getEventByIdQuery(props: GetEventByIdProps) {
    return createQuery<PersistentEvent, ProblemDetails>(() => ({
        enabled: () => !!accessToken.value && !!props.id,
        queryFn: async ({ signal }: { signal: AbortSignal }) => {
            const client = useFetchClient();
            const response = await client.getJSON<PersistentEvent>(`events/${props.id}`, {
                signal
            });

            return response.data!;
        },
        queryKey: queryKeys.id(props.id)
    }));
}

export interface GetEventsByStackIdProps {
    limit?: number;
    stackId: string | undefined;
}

export function getEventsByStackIdQuery(props: GetEventsByStackIdProps) {
    const queryClient = useQueryClient();

    return createQuery<PersistentEvent[], ProblemDetails>(() => ({
        enabled: () => !!accessToken.value && !!props.stackId,
        onSuccess: (data: PersistentEvent[]) => {
            data.forEach((event) => {
                queryClient.setQueryData(queryKeys.id(event.id!), event);
            });
        },
        queryClient,
        queryFn: async ({ signal }: { signal: AbortSignal }) => {
            const client = useFetchClient();
            const response = await client.getJSON<PersistentEvent[]>(`stacks/${props.stackId}/events`, {
                params: {
                    limit: props.limit ?? 10
                },
                signal
            });

            return response.data!;
        },
        queryKey: queryKeys.stacks(props.stackId)
    }));
}
