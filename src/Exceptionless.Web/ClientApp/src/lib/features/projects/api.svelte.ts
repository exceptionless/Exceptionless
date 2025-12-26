import type { ClientConfiguration, NewProject, NotificationSettings, UpdateProject, ViewProject } from '$features/projects/models';
import type { StringValueFromBody } from '$features/shared/models';
import type { WebSocketMessageValue } from '$features/websockets/models';

import { accessToken } from '$features/auth/index.svelte';
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
    } else {
        await queryClient.invalidateQueries({ queryKey: queryKeys.projects() });
    }
}

// TODO: Do we need to scope these all by organization?
export const queryKeys = {
    config: (id: string | undefined) => [...queryKeys.id(id), 'config'] as const,
    deleteConfig: (id: string | undefined) => [...queryKeys.id(id), 'delete-config'] as const,
    deleteProject: (ids: string[] | undefined) => [...queryKeys.ids(ids), 'delete'] as const,
    deletePromotedTab: (id: string | undefined) => [...queryKeys.id(id), 'demote-tab'] as const,
    deleteSlack: (id: string | undefined) => [...queryKeys.id(id), 'delete-slack'] as const,
    id: (id: string | undefined) => [...queryKeys.type, id] as const,
    ids: (ids: string[] | undefined) => [...queryKeys.type, ...(ids ?? [])] as const,
    integrationNotificationSettings: (id: string | undefined, integration: string) => [...queryKeys.id(id), integration, 'notification-settings'] as const,
    organization: (id: string | undefined) => [...queryKeys.type, 'organization', id] as const,
    postConfig: (id: string | undefined) => [...queryKeys.id(id), 'post-config'] as const,
    postProject: () => [...queryKeys.type, 'post-project'] as const,
    postPromotedTab: (id: string | undefined) => [...queryKeys.id(id), 'promote-tab'] as const,
    postSlack: (id: string | undefined) => [...queryKeys.id(id), 'post-slack'] as const,
    postUserNotificationSettings: (id: string | undefined, userId: string | undefined) => [...queryKeys.id(id), userId, 'post-notification-settings'] as const,
    projects: () => [...queryKeys.type, 'projects'] as const,
    putIntegrationNotificationSettings: (id: string | undefined, integration: string) =>
        [...queryKeys.id(id), integration, 'put-notification-settings'] as const,
    resetData: (id: string | undefined) => [...queryKeys.id(id), 'reset-data'] as const,
    type: ['Project'] as const,
    userNotificationSettings: (id: string | undefined, userId: string | undefined) => [...queryKeys.id(id), userId, 'notification-settings'] as const
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

export interface DeleteSlackRequest {
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
    enabled?: () => boolean;
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

export interface GetProjectIntegrationNotificationSettingsRequest {
    route: {
        id: string | undefined;
        integration: string;
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
    limit?: number;
    mode?: GetProjectsMode;
    page?: number;
    sort?: string;
}

export interface GetProjectsRequest {
    params?: GetProjectsParams;
}

export interface GetProjectUserNotificationSettingsRequest {
    route: {
        id: string | undefined;
        userId: string | undefined;
    };
}

export interface PostConfigParams {
    key: string;
    value: string;
}

export interface PostConfigRequest {
    route: {
        id: string | undefined;
    };
}

export interface PostProjectUserNotificationSettingsRequest {
    route: {
        id: string | undefined;
        userId: string | undefined;
    };
}

export interface PostPromotedTabParams {
    name: string;
}

export interface PostPromotedTabRequest {
    route: {
        id: string;
    };
}

export interface PostSlackRequest {
    route: {
        id: string | undefined;
    };
}

export interface PutProjectIntegrationNotificationSettingsRequest {
    route: {
        id: string | undefined;
        integration: string;
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

export function deleteSlack(request: DeleteSlackRequest) {
    const queryClient = useQueryClient();

    return createMutation<boolean, ProblemDetails, void>(() => ({
        enabled: () => !!accessToken.current && request.route.id,
        mutationFn: async () => {
            const client = useFetchClient();
            const response = await client.delete(`projects/${request.route.id}/slack`);

            return response.ok;
        },
        mutationKey: queryKeys.deleteSlack(request.route.id),
        onSuccess: () => {
            queryClient.invalidateQueries({ queryKey: queryKeys.id(request.route.id) });
        }
    }));
}

export function getOrganizationProjectsQuery(request: GetOrganizationProjectsRequest) {
    const queryClient = useQueryClient();

    return createQuery<FetchClientResponse<ViewProject[]>, ProblemDetails>(() => ({
        enabled: () => !!accessToken.current && !!request.route.organizationId && (request.enabled?.() ?? true),
        onSuccess: (data: FetchClientResponse<ViewProject[]>) => {
            data.data?.forEach((project) => {
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
        queryKey: [...queryKeys.organization(request.route.organizationId), { params: request.params }]
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

export function getProjectIntegrationNotificationSettings(request: GetProjectIntegrationNotificationSettingsRequest) {
    return createQuery<NotificationSettings, ProblemDetails>(() => ({
        enabled: () => !!accessToken.current && !!request.route.id,
        queryFn: async ({ signal }: { signal: AbortSignal }) => {
            const client = useFetchClient();
            const response = await client.getJSON<NotificationSettings>(`projects/${request.route.id}/${request.route.integration}/notifications`, {
                signal
            });

            return response.data!;
        },
        queryKey: queryKeys.integrationNotificationSettings(request.route.id, request.route.integration)
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

export function getProjectsQuery(request: GetProjectsRequest) {
    const queryClient = useQueryClient();

    return createQuery<FetchClientResponse<ViewProject[]>, ProblemDetails>(() => ({
        enabled: () => !!accessToken.current,
        onSuccess: (data: FetchClientResponse<ViewProject[]>) => {
            data.data?.forEach((project) => {
                queryClient.setQueryData(queryKeys.id(project.id!), project);
            });
        },
        queryClient,
        queryFn: async ({ signal }: { signal: AbortSignal }) => {
            const client = useFetchClient();
            const response = await client.getJSON<ViewProject[]>('projects', {
                params: {
                    ...request.params,
                    limit: request.params?.limit ?? 1000
                },
                signal
            });

            return response;
        },
        queryKey: [queryKeys.projects(), { params: request.params }]
    }));
}

export function getProjectUserNotificationSettings(request: GetProjectUserNotificationSettingsRequest) {
    return createQuery<NotificationSettings, ProblemDetails>(() => ({
        enabled: () => !!accessToken.current && !!request.route.id && !!request.route.userId,
        queryFn: async ({ signal }: { signal: AbortSignal }) => {
            const client = useFetchClient();
            const response = await client.getJSON<NotificationSettings>(`users/${request.route.userId}/projects/${request.route.id}/notifications`, {
                signal
            });

            return response.data!;
        },
        queryKey: queryKeys.userNotificationSettings(request.route.id, request.route.userId)
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
        mutationKey: queryKeys.postProject(),
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
            const response = await client.post(`projects/${request.route.id}/config`, <StringValueFromBody>{ value: params.value }, {
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

export function postProjectUserNotificationSettings(request: PostProjectUserNotificationSettingsRequest) {
    const queryClient = useQueryClient();

    return createMutation<boolean, ProblemDetails, NotificationSettings>(() => ({
        enabled: () => !!accessToken.current && !!request.route.id && !!request.route.userId,
        mutationFn: async (settings: NotificationSettings) => {
            const client = useFetchClient();
            const response = await client.post(`users/${request.route.userId}/projects/${request.route.id}/notifications`, settings);
            return response.ok;
        },
        mutationKey: queryKeys.postUserNotificationSettings(request.route.id, request.route.userId),
        onSuccess: (_: boolean, variables: NotificationSettings) => {
            queryClient.setQueryData(queryKeys.userNotificationSettings(request.route.id, request.route.userId), variables);
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

export function postSlack(request: PostSlackRequest) {
    const queryClient = useQueryClient();

    return createMutation<boolean, ProblemDetails, string>(() => ({
        enabled: () => !!accessToken.current && !!request.route.id,
        mutationFn: async (code: string) => {
            const client = useFetchClient();
            const response = await client.post(`projects/${request.route.id}/slack`, undefined, { params: { code } });

            return response.ok;
        },
        mutationKey: queryKeys.postSlack(request.route.id),
        onSuccess: () => {
            queryClient.invalidateQueries({ queryKey: queryKeys.integrationNotificationSettings(request.route.id, 'slack') });
        }
    }));
}

export function putProjectIntegrationNotificationSettings(request: PutProjectIntegrationNotificationSettingsRequest) {
    const queryClient = useQueryClient();

    return createMutation<boolean, ProblemDetails, NotificationSettings>(() => ({
        enabled: () => !!accessToken.current && !!request.route.id,
        mutationFn: async (settings: NotificationSettings) => {
            const client = useFetchClient();
            const response = await client.put(`projects/${request.route.id}/${request.route.integration}/notifications`, settings);
            return response.ok;
        },
        mutationKey: queryKeys.putIntegrationNotificationSettings(request.route.id, request.route.integration),
        onSuccess: (_: boolean, variables: NotificationSettings) => {
            queryClient.setQueryData(queryKeys.integrationNotificationSettings(request.route.id, request.route.integration), variables);
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
