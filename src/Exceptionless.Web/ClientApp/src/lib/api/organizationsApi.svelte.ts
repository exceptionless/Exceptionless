import { createQuery, useQueryClient } from '@tanstack/svelte-query';
import type { ViewOrganization } from '$lib/models/api';
import { useFetchClient, type ProblemDetails } from '@exceptionless/fetchclient';
import { derived } from 'svelte/store';
import { accessToken } from '$api/auth.svelte';

export const queryKeys = {
    all: ['Organization'] as const,
    allWithMode: (mode: 'stats' | null) => [...queryKeys.all, { mode }] as const,
    id: (id: string | null) => [...queryKeys.all, id] as const
};

export function getOrganizationQuery(mode: 'stats' | null = null) {
    const queryClient = useQueryClient();
    return createQuery<ViewOrganization[], ProblemDetails>(
        derived(accessToken.value, ($accessToken) => ({
            enabled: !!$accessToken,
            queryClient,
            queryKey: mode ? queryKeys.allWithMode(mode) : queryKeys.all,
            queryFn: async ({ signal }: { signal: AbortSignal }) => {
                const { getJSON } = useFetchClient();
                const response = await getJSON<ViewOrganization[]>('organizations', {
                    signal,
                    params: {
                        mode
                    }
                });

                if (response.ok) {
                    response.data?.forEach((organization) => {
                        queryClient.setQueryData(queryKeys.id(organization.id!), organization);
                    });

                    return response.data!;
                }

                throw response.problem;
            }
        }))
    );
}