import { createQuery } from '@tanstack/svelte-query';
import type { Stack } from '$lib/models/api';
import { FetchClient, type ProblemDetails } from '$api/FetchClient';
import { derived, readable, type Readable } from 'svelte/store';

export const queryKey: string = 'Stack';

export function getStackByIdQuery(id: string | Readable<string | null>) {
    const readableId = typeof id === 'string' ? readable(id) : id;
    return createQuery<Stack, ProblemDetails>(
        derived(readableId, ($id) => ({
            enabled: !!$id,
            queryKey: [queryKey, $id],
            queryFn: async ({ signal }: { signal: AbortSignal }) => {
                const api = new FetchClient();
                const response = await api.getJSON<Stack>(`stacks/${$id}`, {
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
