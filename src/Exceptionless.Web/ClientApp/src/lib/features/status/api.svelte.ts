import { type ProblemDetails, useFetchClient } from '@exceptionless/fetchclient';
import { createQuery } from '@tanstack/svelte-query';

import type { About } from './models';

export const queryKeys = {
    about: ['api/v2/about'] as const,
    health: ['health'] as const
};

export function getAboutQuery() {
    return createQuery<About, ProblemDetails>(() => ({
        queryFn: async ({ signal }: { signal: AbortSignal }) => {
            const client = useFetchClient();
            const response = await client.getJSON<About>('api/v2/about', {
                signal
            });

            if (response.ok) {
                return response.data!;
            }

            throw response.problem;
        },
        queryKey: queryKeys.about,
        staleTime: 12 * 60 * 60 * 1000
    }));
}

export function getHealthQuery() {
    return createQuery<string, ProblemDetails>(() => ({
        queryFn: async ({ signal }: { signal: AbortSignal }) => {
            const client = useFetchClient();
            const response = await client.get(`${globalThis.location.origin}/health`, {
                errorCallback: () => true,
                signal
            });

            if (response.ok) {
                return response.data! as string;
            }

            throw response.problem;
        },
        queryKey: queryKeys.health,
        retry: false,
        staleTime: 30 * 1000
    }));
}
