import { derived } from 'svelte/store';
import { type QueryClient, createQuery } from '@tanstack/svelte-query';

import { FetchClient, type ProblemDetails } from '$api/FetchClient';
import type { User } from '$lib/models/api';
import { accessToken } from '$api/auth';

export const queryKey: string = 'User';

export function getMeQuery(queryClient: QueryClient | undefined = undefined) {
    return createQuery<User, ProblemDetails>(
        derived(accessToken, ($accessToken) => ({
            enabled: !!$accessToken,
            queryKey: [queryKey],
            queryFn: async ({ signal }: { signal: AbortSignal }) => {
                const api = new FetchClient();
                const response = await api.getJSON<User>('users/me', {
                    signal
                });

                if (response.ok) {
                    return response.data!;
                }

                throw response.problem;
            }
        })),
        queryClient
    );
}
