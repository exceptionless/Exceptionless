import type { ClientConfiguration, NewProject, UpdateProject, ViewProject } from '$features/projects/models';
import type { WebSocketMessageValue } from '$features/websockets/models';

import { accessToken } from '$features/auth/index.svelte';
import { ValueFromBody } from '$features/shared/models';
import { type FetchClientResponse, type ProblemDetails, useFetchClient } from '@exceptionless/fetchclient';
import { createMutation, createQuery, QueryClient, useQueryClient } from '@tanstack/svelte-query';

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
    config: (id: string | undefined) => [...queryKeys.id(id), 'config'] as const,
    deleteConfig: (id: string | undefined) => [...queryKeys.id(id), 'delete-config'] as const,
    deleteProject: (ids: string[] | undefined) => [...queryKeys.ids(ids), 'delete'] as const,
    deletePromotedTab: (id: string | undefined) => [...queryKeys.id(id), 'demote-tab'] as const,
    id: (id: string | undefined) => [...queryKeys.type, id] as const,
    ids: (ids: string[] | undefined) => [...queryKeys.type, ...(ids ?? [])] as const,
    organization: (id: string | undefined) => [...queryKeys.type, 'organization', id] as const,
    postConfig: (id: string | undefined) => [...queryKeys.id(id), 'post-config'] as const,
    postPromotedTab: (id: string | undefined) => [...queryKeys.id(id), 'promote-tab'] as const,
    resetData: (id: string | undefined) => [...queryKeys.id(id), 'reset-data'] as const,
    type: ['Project'] as const
};
export interface DeleteConfigParams {
    key: string;
}

export interface DeleteConfigRequest {
    route: {
        id: string | undefined;
    };
}

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

export interface GetProjectConfigRequest {
    route: {
        id: string | undefined;
    };
}

export interface GetProjectRequest {
    route: {
        id: string | undefined;
    };
}

export type GetProjectsMode = 'stats' | null;

export interface PostConfigParams {
    key: string;
    value: string;
}

export interface PostConfigRequest {
    route: {
        id: string | undefined;
    };
}

export interface PostPromotedTabParams {
    name: string;
}

export interface PostPromotedTabRequest {
    route: {
        id: string | undefined;
    };
}

export interface ResetDataRequest {
    route: {
        id: string;
    };
}

export interface UpdateProjectRequest {
    route: {
        id: string;
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

export function deleteProjectConfig(request: DeleteConfigRequest) {
    const queryClient = useQueryClient();

    return createMutation<boolean, ProblemDetails, DeleteConfigParams>(() => ({
        enabled: () => !!accessToken.current,
        mutationFn: async (params: DeleteConfigParams) => {
            const client = useFetchClient();
            const response = await client.delete(`projects/${request.route.id}/config`, {
                params: { key: params.key }
            });

            return response.ok;
        },
        mutationKey: queryKeys.deleteConfig(request.route.id),
        onSuccess: () => {
            queryClient.invalidateQueries({ queryKey: queryKeys.config(request.route.id) });
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

export function getProjectConfig(request: GetProjectConfigRequest) {
    return createQuery<ClientConfiguration, ProblemDetails>(() => ({
        enabled: () => !!accessToken.current && !!request.route.id,
        queryFn: async ({ signal }: { signal: AbortSignal }) => {
            const client = useFetchClient();
            const response = await client.getJSON<ClientConfiguration>(`projects/${request.route.id}/config`, {
                signal
            });

            return response.data!;
        },
        queryKey: queryKeys.config(request.route.id)
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

export function postProjectConfig(request: PostConfigRequest) {
    const queryClient = useQueryClient();

    return createMutation<boolean, ProblemDetails, PostConfigParams>(() => ({
        enabled: () => !!accessToken.current,
        mutationFn: async (params: PostConfigParams) => {
            const client = useFetchClient();
            const response = await client.post(`projects/${request.route.id}/config`, new ValueFromBody(params.value), {
                params: { key: params.key }
            });

            return response.ok;
        },
        mutationKey: queryKeys.postConfig(request.route.id),
        onSuccess: () => {
            queryClient.invalidateQueries({ queryKey: queryKeys.config(request.route.id) });
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

export function resetData(request: ResetDataRequest) {
    return createMutation<void, ProblemDetails, void>(() => ({
        enabled: () => !!accessToken.current && !!request.route.id,
        mutationFn: async () => {
            const client = useFetchClient();
            await client.post(`projects/${request.route.id}/reset-data`, undefined, {
                expectedStatusCodes: [202]
            });
        },
        mutationKey: queryKeys.resetData(request.route.id)
    }));
}

export function updateProject(request: UpdateProjectRequest) {
    const queryClient = useQueryClient();

    return createMutation<ViewProject, ProblemDetails, UpdateProject>(() => ({
        enabled: () => !!accessToken.current && !!request.route.id,
        mutationFn: async (data: UpdateProject) => {
            const client = useFetchClient();
            const response = await client.patchJSON<ViewProject>(`projects/${request.route.id}`, data);
            return response.data!;
        },
        onError: () => {
            queryClient.invalidateQueries({ queryKey: queryKeys.id(request.route.id) });
        },
        onSuccess: (project: ViewProject) => {
            queryClient.setQueryData(queryKeys.id(request.route.id), project);
        }
    }));
        }

