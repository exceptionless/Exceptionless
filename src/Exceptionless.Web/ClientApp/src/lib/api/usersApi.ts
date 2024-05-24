import { derived } from 'svelte/store';
import { createQuery, useQueryClient } from '@tanstack/svelte-query';

import { useFetchClient, type ProblemDetails } from '@exceptionless/fetchclient';
import type { User } from '$lib/models/api';
import { accessToken } from '$api/auth.svelte';

export const queryKeys = {
    all: ['User'] as const,
    id: (id: string | null) => [...queryKeys.all, id] as const,
    me: () => [...queryKeys.all, 'me'] as const
};

export function getMeQuery() {
    const queryClient = useQueryClient();
    return createQuery<User, ProblemDetails>(
        derived(accessToken, ($accessToken) => ({
            enabled: !!$accessToken,
            queryClient,
            queryKey: queryKeys.me(),
            queryFn: async ({ signal }: { signal: AbortSignal }) => {
                const { getJSON } = useFetchClient();
                const response = await getJSON<User>('users/me', {
                    signal
                });

                if (response.ok) {
                    queryClient.setQueryData(queryKeys.id(response.data!.id!), response.data);
                    return response.data!;
                }

                throw response.problem;
            }
        }))
    );
}
