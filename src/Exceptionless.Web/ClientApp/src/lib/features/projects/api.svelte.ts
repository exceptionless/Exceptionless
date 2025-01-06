import type { WebSocketMessageValue } from '$features/websockets/models';

import { accessToken } from '$features/auth/index.svelte';
import { type ProblemDetails, useFetchClient } from '@exceptionless/fetchclient';
import { createMutation, createQuery, QueryClient, useQueryClient } from '@tanstack/svelte-query';

import type { ViewProject } from './models';

export async function invalidateProjectQueries(queryClient: QueryClient, message: WebSocketMessageValue<'ProjectChanged'>) {
    const { id, organization_id } = message;
    if (id) {
        await queryClient.invalidateQueries({ queryKey: queryKeys.id(id) });
    }

    if (organization_id) {
        await queryClient.invalidateQueries({ queryKey: queryKeys.organization(organization_id) });
    }

    if (!id && !organization_id) {
        await queryClient.invalidateQueries({ queryKey: queryKeys.type });
    }
}

export const queryKeys = {
    deletePromotedTab: (id: string | undefined) => [...queryKeys.id(id), 'demote-tab'] as const,
    id: (id: string | undefined) => [...queryKeys.type, id] as const,
    organization: (id: string | undefined) => [...queryKeys.type, 'organization', id] as const,
    postPromotedTab: (id: string | undefined) => [...queryKeys.id(id), 'promote-tab'] as const,
    type: ['Project'] as const
};

export interface deletePromotedTabRequest {
    route: {
        id: string;
    };
}

export interface GetOrganizationProjectsRequest {
    params?: {
        filter?: string;
        limit?: number;
        mode?: 'stats';
        page?: number;
        sort?: string;
    };
    route: {
        organizationId: string | undefined;
    };
}

export interface GetProjectRequest {
    route: {
        id: string | undefined;
    };
}

export interface PostPromotedTabRequest {
    route: {
        id: string | undefined;
    };
}

export function deletePromotedTab(request: deletePromotedTabRequest) {
    const queryClient = useQueryClient();
    return createMutation<boolean, ProblemDetails, { name: string }>(() => ({
        mutationFn: async (params: { name: string }) => {
            const client = useFetchClient();
            const response = await client.delete(`projects/${request.route.id}/promotedtabs`, {
                params
            });

            return response.ok;
        },
        mutationKey: queryKeys.deletePromotedTab(request.route.id),
        onError: () => {
            queryClient.invalidateQueries({ queryKey: queryKeys.id(request.route.id) });
        },
        onSuccess: (_: boolean, variables: { name: string }) => {
            // Update the project to reflect the demoted tab until it's updated from the server
            const previousProject = queryClient.getQueryData<ViewProject>(queryKeys.id(request.route.id));
            if (previousProject) {
                queryClient.setQueryData(queryKeys.id(request.route.id), {
                    ...previousProject,
                    promoted_tabs: (previousProject?.promoted_tabs ?? []).filter((tab) => tab !== variables.name)
                });
            }
        }
    }));
}

export function getOrganizationProjectsQuery(request: GetOrganizationProjectsRequest) {
    const queryClient = useQueryClient();

    return createQuery<ViewProject[], ProblemDetails>(() => ({
        enabled: () => !!accessToken.value && !!request.route.organizationId,
        onSuccess: (data: ViewProject[]) => {
            data.forEach((project) => {
                queryClient.setQueryData(queryKeys.id(project.id!), project);
            });
        },
        queryClient,
        queryFn: async ({ signal }: { signal: AbortSignal }) => {
            const client = useFetchClient();
            const response = await client.getJSON<ViewProject[]>(`organizations/${request.route.organizationId}/projects`, {
                params: {
                    ...request.params,
                    limit: request.params?.limit ?? 1000
                },
                signal
            });

            return response.data!;
        },
        queryKey: queryKeys.organization(request.route.organizationId)
    }));
}

export function getProjectQuery(request: GetProjectRequest) {
    return createQuery<ViewProject, ProblemDetails>(() => ({
        enabled: () => !!accessToken.value && !!request.route.id,
        queryFn: async ({ signal }: { signal: AbortSignal }) => {
            const client = useFetchClient();
            const response = await client.getJSON<ViewProject>(`projects/${request.route.id}`, {
                signal
            });

            return response.data!;
        },
        queryKey: queryKeys.id(request.route.id)
    }));
}

export function postPromotedTab(request: PostPromotedTabRequest) {
    const queryClient = useQueryClient();
    return createMutation<boolean, ProblemDetails, { name: string }>(() => ({
        mutationFn: async (params: { name: string }) => {
            const client = useFetchClient();
            const response = await client.post(`projects/${request.route.id}/promotedtabs`, undefined, {
                params
            });

            return response.ok;
        },
        mutationKey: queryKeys.postPromotedTab(request.route.id),
        onError: () => {
            queryClient.invalidateQueries({ queryKey: queryKeys.id(request.route.id) });
        },
        onSuccess: (_: boolean, variables: { name: string }) => {
            // Update the project to reflect the new promoted tab until it's updated from the server
            const previousProject = queryClient.getQueryData<ViewProject>(queryKeys.id(request.route.id));
            if (previousProject) {
                queryClient.setQueryData(queryKeys.id(request.route.id), {
                    ...previousProject,
                    promoted_tabs: [...(previousProject?.promoted_tabs ?? []), variables.name]
                });
            }
        }
    }));
}
