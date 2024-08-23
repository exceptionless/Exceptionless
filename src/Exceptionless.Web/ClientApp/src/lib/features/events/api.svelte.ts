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

export function getEventByIdQuery(props: GetEventByIdProps) {
    return createQuery<PersistentEvent, ProblemDetails>(() => ({
        enabled: !!accessToken.value && !!props.id,
        queryFn: async ({ signal }: { signal: AbortSignal }) => {
            const client = useFetchClient();
            const response = await client.getJSON<PersistentEvent>(`events/${props.id}`, {
                signal
            });

            if (response.ok) {
                return response.data!;
            }

            throw response.problem;
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
        enabled: !!accessToken.value && !!props.stackId,
        queryClient,
        queryFn: async ({ signal }: { signal: AbortSignal }) => {
            const client = useFetchClient();
            const response = await client.getJSON<PersistentEvent[]>(`stacks/${props.stackId}/events`, {
                params: {
                    limit: props.limit ?? 10
                },
                signal
            });

            if (response.ok) {
                response.data?.forEach((event) => {
                    queryClient.setQueryData(queryKeys.id(event.id!), event);
                });

                return response.data!;
            }

            throw response.problem;
        },
        queryKey: queryKeys.stacks(props.stackId)
    }));
}
