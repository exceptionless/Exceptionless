import { createQuery, useQueryClient } from '@tanstack/svelte-query';
import { derived, get, readable, type Readable } from 'svelte/store';
import type { Stack } from '$lib/models/api';
import { FetchClient, type ProblemDetails } from '$api/FetchClient';
import { accessToken } from '$api/auth';

export const queryKeys = {
    all: ['Stack'] as const,
    allWithFilters: (filters: string) => [...queryKeys.all, { filters }] as const,
    id: (id: string | null) => [...queryKeys.all, id] as const
};

export async function prefetchStack(id: string) {
    if (!get(accessToken)) {
        return;
    }

    const queryClient = useQueryClient();
    await queryClient.prefetchQuery<Stack, ProblemDetails>({
        queryKey: queryKeys.id(id),
        queryFn: async ({ signal }: { signal: AbortSignal }) => {
            const { getJSON } = new FetchClient();
            const response = await getJSON<Stack>(`stacks/${id}`, {
                signal
            });

            if (response.ok) {
                return response.data!;
            }

            throw response.problem;
        }
    });
}

export function getStackByIdQuery(id: string | Readable<string | null>) {
    const readableId = typeof id === 'string' || id === null ? readable(id) : id;
    return createQuery<Stack, ProblemDetails>(
        derived([accessToken, readableId], ([$accessToken, $id]) => ({
            enabled: !!$accessToken && !!$id,
            queryKey: queryKeys.id($id),
            queryFn: async ({ signal }: { signal: AbortSignal }) => {
                const { getJSON } = new FetchClient();
                const response = await getJSON<Stack>(`stacks/${$id}`, {
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
