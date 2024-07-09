import { createQuery, useQueryClient } from '@tanstack/svelte-query';

import { useFetchClient, type ProblemDetails } from '@exceptionless/fetchclient';
import type { User } from '$lib/models/api';
import { accessToken } from '$api/auth.svelte';

export const queryKeys = {
    all: ['User'] as const,
    id: (id: string | undefined) => [...queryKeys.all, id] as const,
    me: () => [...queryKeys.all, 'me'] as const
};

export function getMeQuery() {
    const queryClient = useQueryClient();
    const queryOptions = $derived({
        enabled: !!accessToken.value,
        queryClient,
        queryKey: queryKeys.me(),
        queryFn: async ({ signal }: { signal: AbortSignal }) => {
            const client = useFetchClient();
            const response = await client.getJSON<User>('users/me', {
                signal
            });

            if (response.ok) {
                queryClient.setQueryData(queryKeys.id(response.data!.id!), response.data);
                return response.data!;
            }

            throw response.problem;
        }
    });

    return createQuery<User, ProblemDetails>(queryOptions);
}
