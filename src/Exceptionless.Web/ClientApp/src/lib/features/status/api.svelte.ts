import { type ProblemDetails, useFetchClient } from '@exceptionless/fetchclient';
import { createQuery } from '@tanstack/svelte-query';

import type { About } from './models';

export const queryKeys = {
    about: ['about'] as const,
    health: ['health'] as const
};

export function getAboutQuery() {
    return createQuery<About, ProblemDetails>(() => ({
        queryFn: async ({ signal }: { signal: AbortSignal }) => {
            const client = useFetchClient();
            const response = await client.getJSON<About>('about', {
                signal
            });

            return response.data!;
        },
        queryKey: queryKeys.about,
        staleTime: 12 * 60 * 60 * 1000
    }));
}

export function getHealthQuery() {
    return createQuery<string, ProblemDetails>(() => ({
        queryFn: async ({ signal }: { signal: AbortSignal }) => {
            const client = useFetchClient({ baseUrl: '' });
            const response = await client.get('health', {
                signal
            });

            return response.data! as string;
        },
        queryKey: queryKeys.health,
        retry: false,
        staleTime: 30 * 1000
    }));
}
