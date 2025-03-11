import type { WebSocketMessageValue } from '$features/websockets/models';

import { accessToken } from '$features/auth/index.svelte';
import { type FetchClientResponse, type ProblemDetails, useFetchClient } from '@exceptionless/fetchclient';
import { createMutation, createQuery, QueryClient, useQueryClient } from '@tanstack/svelte-query';

import type { NewProject, ViewProject } from './models';

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

// TODO: Do we need to scope these all by organization?
export const queryKeys = {
    deleteProject: (ids: string[] | undefined) => [...queryKeys.ids(ids), 'delete'] as const,
    deletePromotedTab: (id: string | undefined) => [...queryKeys.id(id), 'demote-tab'] as const,
    id: (id: string | undefined) => [...queryKeys.type, id] as const,
    ids: (ids: string[] | undefined) => [...queryKeys.type, ...(ids ?? [])] as const,
    organization: (id: string | undefined) => [...queryKeys.type, 'organization', id] as const,
    postPromotedTab: (id: string | undefined) => [...queryKeys.id(id), 'promote-tab'] as const,
    type: ['Project'] as const
};

export interface DeleteProjectRequest {
    route: {
        ids: string[];
    };
}

export interface DeletePromotedTabParams {
    name: string;
}

export interface DeletePromotedTabRequest {
    route: {
        id: string;
    };
}

export interface GetOrganizationProjectsParams {
        filter?: string;
        limit?: number;
    mode?: GetProjectsMode;
        page?: number;
        sort?: string;
}

export interface GetOrganizationProjectsRequest {
    params?: GetOrganizationProjectsParams;
    route: {
        organizationId: string | undefined;
    };
}

export interface GetProjectRequest {
    route: {
        id: string | undefined;
    };
}

export type GetProjectsMode = 'stats' | null;

export interface GetProjectsParams {
    filter?: string;
}

export interface PostPromotedTabParams {
    name: string;
}

export interface PostPromotedTabRequest {
    route: {
        id: string | undefined;
    };
}

export function deleteProject(request: DeleteProjectRequest) {
    const queryClient = useQueryClient();

    return createMutation<FetchClientResponse<unknown>, ProblemDetails, void>(() => ({
        enabled: () => !!accessToken.current && !!request.route.ids?.length,
        mutationFn: async () => {
            const client = useFetchClient();
            const response = await client.delete(`projects/${request.route.ids?.join(',')}`, {
                expectedStatusCodes: [202]
            });

            return response;
        },
        mutationKey: queryKeys.deleteProject(request.route.ids),
        onError: () => {
            request.route.ids?.forEach((id) => queryClient.invalidateQueries({ queryKey: queryKeys.id(id) }));
        },
        onSuccess: () => {
            request.route.ids?.forEach((id) => queryClient.invalidateQueries({ queryKey: queryKeys.id(id) }));
        }
    }));
}

export function deletePromotedTab(request: DeletePromotedTabRequest) {
    const queryClient = useQueryClient();
    return createMutation<boolean, ProblemDetails, DeletePromotedTabParams>(() => ({
        mutationFn: async (params: DeletePromotedTabParams) => {
            const client = useFetchClient();
            const response = await client.delete(`projects/${request.route.id}/promotedtabs`, {
                params: { ...params }
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

    return createQuery<FetchClientResponse<ViewProject[]>, ProblemDetails>(() => ({
        enabled: () => !!accessToken.current && !!request.route.organizationId,
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

            return response;
        },
        queryKey: queryKeys.organization(request.route.organizationId)
    }));
}

export function getProjectQuery(request: GetProjectRequest) {
    return createQuery<ViewProject, ProblemDetails>(() => ({
        enabled: () => !!accessToken.current && !!request.route.id,
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
export function postProject() {
    const queryClient = useQueryClient();

    return createMutation<ViewProject, ProblemDetails, NewProject>(() => ({
        enabled: () => !!accessToken.current,
        mutationFn: async (project: NewProject) => {
            const client = useFetchClient();
            const response = await client.postJSON<ViewProject>('projects', project);
            return response.data!;
        },
        onSuccess: () => {
            queryClient.invalidateQueries({ queryKey: queryKeys.type });
        }
    }));
}

export function postPromotedTab(request: PostPromotedTabRequest) {
    const queryClient = useQueryClient();
    return createMutation<boolean, ProblemDetails, PostPromotedTabParams>(() => ({
        mutationFn: async (params: PostPromotedTabParams) => {
            const client = useFetchClient();
            const response = await client.post(`projects/${request.route.id}/promotedtabs`, undefined, {
                params: { ...params }
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
