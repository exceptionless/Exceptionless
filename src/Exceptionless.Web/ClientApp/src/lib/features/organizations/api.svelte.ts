import { accessToken } from '$features/auth/index.svelte';
import { type ProblemDetails, useFetchClient } from '@exceptionless/fetchclient';
import { createQuery, useQueryClient } from '@tanstack/svelte-query';

import type { ViewOrganization } from './models';

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
        enabled: () => !!accessToken.value,
        onSuccess: (data: ViewOrganization[]) => {
            data.forEach((organization) => {
                queryClient.setQueryData(queryKeys.id(organization.id!), organization);
            });
        },
        queryClient,
        queryFn: async ({ signal }: { signal: AbortSignal }) => {
            const client = useFetchClient();
            const response = await client.getJSON<ViewOrganization[]>('organizations', {
                params: {
                    mode: props.mode
                },
                signal
            });

            return response.data!;
        },
        queryKey: props.mode ? queryKeys.allWithMode(props.mode) : queryKeys.all
    }));
}
