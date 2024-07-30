import { createQuery, useQueryClient } from '@tanstack/svelte-query';
import type { ViewOrganization } from '$lib/models/api';
import { useFetchClient, type ProblemDetails } from '@exceptionless/fetchclient';
import { accessToken } from '$api/auth.svelte';

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
        queryKey: props.mode ? queryKeys.allWithMode(props.mode) : queryKeys.all,
        queryFn: async ({ signal }: { signal: AbortSignal }) => {
            const client = useFetchClient();
            const response = await client.getJSON<ViewOrganization[]>('organizations', {
                signal,
                params: {
                    mode: props.mode
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
    }));
}
