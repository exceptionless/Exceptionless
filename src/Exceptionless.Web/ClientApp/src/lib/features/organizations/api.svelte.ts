import type { WebSocketMessageValue } from '$features/websockets/models';
import type { QueryClient } from '@tanstack/svelte-query';

import { accessToken } from '$features/auth/index.svelte';
import { type ProblemDetails, useFetchClient } from '@exceptionless/fetchclient';
import { createQuery, useQueryClient } from '@tanstack/svelte-query';

import type { ViewOrganization } from './models';

export async function invalidateOrganizationQueries(queryClient: QueryClient, message: WebSocketMessageValue<'OrganizationChanged'>) {
    const { id } = message;
    if (id) {
        await queryClient.invalidateQueries({ queryKey: queryKeys.id(id) });

        // Invalidate regardless of mode
        await queryClient.invalidateQueries({ queryKey: queryKeys.list(undefined) });
    } else {
        await queryClient.invalidateQueries({ queryKey: queryKeys.type });
    }
}

export const queryKeys = {
    id: (id: string | undefined) => [...queryKeys.type, id] as const,
    list: (mode: 'stats' | undefined) => (mode ? ([...queryKeys.type, 'list', { mode }] as const) : ([...queryKeys.type, 'list'] as const)),
    type: ['Organization'] as const
};

export interface GetOrganizationsRequest {
    params?: {
        mode: 'stats' | null;
    };
}

export function getOrganizationQuery(request: GetOrganizationsRequest) {
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
                params: request.params,
                signal
            });

            return response.data!;
        },
        queryKey: queryKeys.list(request.params?.mode ?? undefined)
    }));
}
