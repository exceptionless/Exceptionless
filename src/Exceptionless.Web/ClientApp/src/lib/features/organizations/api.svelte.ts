import type { WebSocketMessageValue } from '$features/websockets/models';
import type { QueryClient } from '@tanstack/svelte-query';

import { accessToken } from '$features/auth/index.svelte';
import { type ProblemDetails, useFetchClient } from '@exceptionless/fetchclient';
import { createQuery, useQueryClient } from '@tanstack/svelte-query';

import type { ViewOrganization } from './models';

export async function invalidateOrganizationQueries(queryClient: QueryClient, message: WebSocketMessageValue<'OrganizationChanged'>) {
    const { id } = message;
    if (id) {
        await queryClient.invalidateQueries({ queryKey: queryKeys.id(id, undefined) });
        await queryClient.invalidateQueries({ queryKey: queryKeys.id(id, 'stats') });

        // Invalidate regardless of mode
        await queryClient.invalidateQueries({ queryKey: queryKeys.list(undefined) });
    } else {
        await queryClient.invalidateQueries({ queryKey: queryKeys.type });
    }
}

export const queryKeys = {
    id: (id: string | undefined, mode: 'stats' | undefined) => (mode ? ([...queryKeys.type, id, { mode }] as const) : ([...queryKeys.type, id] as const)),
    list: (mode: 'stats' | undefined) => (mode ? ([...queryKeys.type, 'list', { mode }] as const) : ([...queryKeys.type, 'list'] as const)),
    type: ['Organization'] as const
};

export interface GetOrganizationRequest {
    params?: {
        mode: 'stats' | undefined;
    };
    route: {
        id: string | undefined;
    };
}

export interface GetOrganizationsRequest {
    params?: {
        mode: 'stats' | undefined;
    };
}

export function getOrganizationQuery(request: GetOrganizationRequest) {
    const queryClient = useQueryClient();

    return createQuery<ViewOrganization, ProblemDetails>(() => ({
        enabled: () => !!accessToken.current && !!request.route.id,
        onSuccess: (data: ViewOrganization) => {
            if (request.params?.mode) {
                queryClient.setQueryData(queryKeys.id(request.route.id, request.params.mode), data);
            }

            queryClient.setQueryData(queryKeys.id(request.route.id!, undefined), data);
        },
        queryClient,
        queryFn: async ({ signal }: { signal: AbortSignal }) => {
            const client = useFetchClient();
            const response = await client.getJSON<ViewOrganization>(`organizations/${request.route.id}`, {
                signal
            });

            return response.data!;
        },
        queryKey: queryKeys.id(request.route.id, request.params?.mode)
    }));
}

export function getOrganizationsQuery(request: GetOrganizationsRequest) {
    const queryClient = useQueryClient();

    return createQuery<ViewOrganization[], ProblemDetails>(() => ({
        enabled: () => !!accessToken.current,
        onSuccess: (data: ViewOrganization[]) => {
            data.forEach((organization) => {
                if (request.params?.mode) {
                    queryClient.setQueryData(queryKeys.id(organization.id!, request.params.mode), organization);
                }

                queryClient.setQueryData(queryKeys.id(organization.id!, undefined), organization);
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
        queryKey: [...queryKeys.list(request.params?.mode ?? undefined), { params: request.params }]
    }));
}
