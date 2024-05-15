import { createQuery, useQueryClient } from '@tanstack/svelte-query';
import type { PersistentEvent } from '$lib/models/api';
import { FetchClient, type ProblemDetails } from '$api/FetchClient.svelte';
import { derived, readable, type Readable } from 'svelte/store';
import { accessToken } from '$api/auth.svelte';

export const queryKeys = {
    all: ['PersistentEvent'] as const,
    allWithFilters: (filters: string) => [...queryKeys.all, { filters }] as const,
    stacks: (id: string | null) => [...queryKeys.all, 'stacks', id] as const,
    stackWithFilters: (id: string | null, filters: string) => [...queryKeys.stacks(id), { filters }] as const,
    id: (id: string | null) => [...queryKeys.all, id] as const
};

// UPGRADE
export function getEventByIdQuery(id: string | Readable<string | null>) {
    const readableId = typeof id === 'string' || id === null ? readable(id) : id;
    return createQuery<PersistentEvent, ProblemDetails>(
        derived([accessToken.value, readableId], ([$accessToken, $id]) => ({
            enabled: !!$accessToken && !!$id,
            queryKey: queryKeys.id($id),
            queryFn: async ({ signal }: { signal: AbortSignal }) => {
                const { getJSON } = new FetchClient();
                const response = await getJSON<PersistentEvent>(`events/${$id}`, {
                    signal
                });

                if (response.ok) {
                    return response.data!;
                }

                throw response.problem;
            }
        }))
    );
}

export function getEventsByStackIdQuery(stackId: string | Readable<string | null>, limit: number = 10) {
    const queryClient = useQueryClient();
    const readableStackId = typeof stackId === 'string' || stackId === null ? readable(stackId) : stackId;
    return createQuery<PersistentEvent[], ProblemDetails>(
        derived([accessToken, readableStackId], ([$accessToken, $id]) => ({
            enabled: !!$accessToken && !!$id,
            queryClient,
            queryKey: queryKeys.stacks($id),
            queryFn: async ({ signal }: { signal: AbortSignal }) => {
                const { getJSON } = new FetchClient();
                const response = await getJSON<PersistentEvent[]>(`stacks/${$id}/events`, {
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
        }))
    );
}
