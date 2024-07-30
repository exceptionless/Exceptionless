import type { ViewOrganization } from '$lib/models/api';

import { accessToken } from '$api/auth.svelte';
import { type ProblemDetails, useFetchClient } from '@exceptionless/fetchclient';
import { createQuery, useQueryClient } from '@tanstack/svelte-query';

export const queryKeys = {
    all: ['Organization'] as const,
    allWithMode: (mode: 'stats' | undefined) => [...queryKeys.all, { mode }] as const,
    id: (id: string | undefined) => [...queryKeys.all, id] as const
};

export interface GetOrganizationsProps {
    mode: 'stats' | null;
}

export function getOrganizationQuery(props: GetOrganizationsProps) {
    const queryClient = useQueryClient();

    return createQuery<ViewOrganization[], ProblemDetails>(() => ({
        enabled: !!accessToken.value,
        queryClient,
        queryFn: async ({ signal }: { signal: AbortSignal }) => {
            const client = useFetchClient();
            const response = await client.getJSON<ViewOrganization[]>('organizations', {
                params: {
                    mode: props.mode
                },
                signal
            });

            if (response.ok) {
                response.data?.forEach((organization) => {
                    queryClient.setQueryData(queryKeys.id(organization.id!), organization);
                });

                return response.data!;
            }

            throw response.problem;
        },
        queryKey: props.mode ? queryKeys.allWithMode(props.mode) : queryKeys.all
    }));
}
