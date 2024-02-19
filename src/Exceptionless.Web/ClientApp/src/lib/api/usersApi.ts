import { derived } from 'svelte/store';
import { createQuery } from '@tanstack/svelte-query';

import { FetchClient, type ProblemDetails } from '$api/FetchClient';
import type { User } from '$lib/models/api';
import { accessToken } from '$api/auth';

export const queryKey: string = 'User';

export function getMeQuery() {
    return createQuery<User, ProblemDetails>(
        derived(accessToken, ($accessToken) => ({
            enabled: !!$accessToken,
            queryKey: [queryKey],
            queryFn: async ({ signal }: { signal: AbortSignal }) => {
                const { getJSON } = new FetchClient();
                const response = await getJSON<User>('users/me', {
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
