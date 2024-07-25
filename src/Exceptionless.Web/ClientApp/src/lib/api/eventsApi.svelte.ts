import { createQuery, useQueryClient } from '@tanstack/svelte-query';
import type { PersistentEvent } from '$lib/models/api';
import { useFetchClient, type ProblemDetails } from '@exceptionless/fetchclient';
import { accessToken } from '$api/auth.svelte';

export const queryKeys = {
    all: ['PersistentEvent'] as const,
    allWithFilters: (filters: string) => [...queryKeys.all, { filters }] as const,
    stacks: (id: string | undefined) => [...queryKeys.all, 'stacks', id] as const,
    stackWithFilters: (id: string | undefined, filters: string) => [...queryKeys.stacks(id), { filters }] as const,
    id: (id: string | undefined) => [...queryKeys.all, id] as const
};

export interface GetEventByIdProps {
    id: string | undefined;
}

export function getEventByIdQuery(props: GetEventByIdProps) {
    const queryOptions = $derived({
        enabled: !!accessToken.value && !!props.id,
        queryKey: queryKeys.id(props.id),
        queryFn: async ({ signal }: { signal: AbortSignal }) => {
            const client = useFetchClient();
            const response = await client.getJSON<PersistentEvent>(`events/${props.id}`, {
                signal
            });

            if (response.ok) {
                return response.data!;
            }

            throw response.problem;
        }
    });

    return createQuery<PersistentEvent, ProblemDetails>(() => queryOptions);
}

export interface GetEventsByStackIdProps {
    stackId: string | undefined;
    limit?: number;
}

export function getEventsByStackIdQuery(props: GetEventsByStackIdProps) {
    const queryClient = useQueryClient();
    const queryOptions = $derived({
        enabled: !!accessToken.value && !!props.stackId,
        queryClient,
        queryKey: queryKeys.stacks(props.stackId),
        queryFn: async ({ signal }: { signal: AbortSignal }) => {
            const client = useFetchClient();
            const response = await client.getJSON<PersistentEvent[]>(`stacks/${props.stackId}/events`, {
                signal,
                params: {
                    limit: props.limit ?? 10
                }
            });

            if (response.ok) {
                response.data?.forEach((event) => {
                    queryClient.setQueryData(queryKeys.id(event.id!), event);
                });

                return response.data!;
            }

            throw response.problem;
        }
    });

    return createQuery<PersistentEvent[], ProblemDetails>(() => queryOptions);
}
