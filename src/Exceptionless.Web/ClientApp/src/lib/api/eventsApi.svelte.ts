import { createQuery, useQueryClient } from '@tanstack/svelte-query-runes';
import type { PersistentEvent } from '$lib/models/api';
import { useFetchClient, type ProblemDetails } from '@exceptionless/fetchclient';
import { accessToken } from '$api/auth.svelte';

export const queryKeys = {
    all: ['PersistentEvent'] as const,
    allWithFilters: (filters: string) => [...queryKeys.all, { filters }] as const,
    stacks: (id: string | null) => [...queryKeys.all, 'stacks', id] as const,
    stackWithFilters: (id: string | null, filters: string) => [...queryKeys.stacks(id), { filters }] as const,
    id: (id: string | null) => [...queryKeys.all, id] as const
};

export function getEventByIdQuery(id: string) {
    const queryOptions = $derived({
        enabled: !!accessToken.value && !!id,
        queryKey: queryKeys.id(id),
        queryFn: async ({ signal }: { signal: AbortSignal }) => {
            const client = useFetchClient();
            const response = await client.getJSON<PersistentEvent>(`events/${id}`, {
                signal
            });

            if (response.ok) {
                return response.data!;
            }

            throw response.problem;
        }
    });

    return createQuery<PersistentEvent, ProblemDetails>(queryOptions);
}

export function getEventsByStackIdQuery(stackId: string | null, limit: number = 10) {
    const queryClient = useQueryClient();
    const queryOptions = $derived({
        enabled: !!accessToken.value && !!stackId,
        queryClient,
        queryKey: queryKeys.stacks(stackId),
        queryFn: async ({ signal }: { signal: AbortSignal }) => {
            const client = useFetchClient();
            const response = await client.getJSON<PersistentEvent[]>(`stacks/${stackId}/events`, {
                signal,
                params: {
                    limit
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

    return createQuery<PersistentEvent[], ProblemDetails>(queryOptions);
}
