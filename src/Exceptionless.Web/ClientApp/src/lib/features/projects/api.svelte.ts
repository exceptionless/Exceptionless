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
    id: (id: string | undefined) => [...queryKeys.type, id] as const,
    organization: (id: string | undefined) => [...queryKeys.type, 'organization', id] as const,
    type: ['Project'] as const
};

export interface DemoteProjectTabProps {
    id: string;
}

export interface GetProjectByIdProps {
    id: string | undefined;
}

export interface GetProjectsByOrganizationIdProps {
    limit?: number;
    organizationId: string | undefined;
}

export interface PromoteProjectTabProps {
    id: string;
}

export function getProjectByIdQuery(props: GetProjectByIdProps) {
    return createQuery<ViewProject, ProblemDetails>(() => ({
        enabled: () => !!accessToken.value && !!props.id,
        queryFn: async ({ signal }: { signal: AbortSignal }) => {
            const client = useFetchClient();
            const response = await client.getJSON<ViewProject>(`projects/${props.id}`, {
                signal
            });

            return response.data!;
        },
        queryKey: queryKeys.id(props.id)
    }));
}

export function getProjectsByOrganizationIdQuery(props: GetProjectsByOrganizationIdProps) {
    const queryClient = useQueryClient();

    return createQuery<ViewProject[], ProblemDetails>(() => ({
        enabled: () => !!accessToken.value && !!props.organizationId,
        onSuccess: (data: ViewProject[]) => {
            data.forEach((project) => {
                queryClient.setQueryData(queryKeys.id(project.id!), project);
            });
        },
        queryClient,
        queryFn: async ({ signal }: { signal: AbortSignal }) => {
            const client = useFetchClient();
            const response = await client.getJSON<ViewProject[]>(`organizations/${props.organizationId}/projects`, {
                params: {
                    limit: props.limit ?? 1000
                },
                signal
            });

            return response.data!;
        },
        queryKey: queryKeys.organization(props.organizationId)
    }));
}

export function mutateDemoteTab(props: DemoteProjectTabProps) {
    const queryClient = useQueryClient();
    return createMutation<boolean, ProblemDetails, { name: string }>(() => ({
        mutationFn: async (params: { name: string }) => {
            const client = useFetchClient();
            const response = await client.delete(`projects/${props.id}/promotedtabs`, {
                params
            });

            // TODO: Fix status code returns.
            return response.ok;
        },
        mutationKey: queryKeys.id(props.id),
        onError: () => {
            queryClient.invalidateQueries({ queryKey: queryKeys.id(props.id) });
        },
        onSuccess: (_: boolean, variables: { name: string }) => {
            // Update the project to reflect the demoted tab until it's updated from the server
            const previousProject = queryClient.getQueryData<ViewProject>(queryKeys.id(props.id));
            if (previousProject) {
                queryClient.setQueryData(queryKeys.id(props.id), {
                    ...previousProject,
                    promoted_tabs: (previousProject?.promoted_tabs ?? []).filter((tab) => tab !== variables.name)
                });
            }
        }
    }));
}

export function mutatePromoteTab(props: PromoteProjectTabProps) {
    const queryClient = useQueryClient();
    return createMutation<boolean, ProblemDetails, { name: string }>(() => ({
        mutationFn: async (params: { name: string }) => {
            const client = useFetchClient();
            const response = await client.post(`projects/${props.id}/promotedtabs`, undefined, {
                params
            });

            // TODO: Fix status code returns.
            return response.ok;
        },
        mutationKey: queryKeys.id(props.id),
        onError: () => {
            queryClient.invalidateQueries({ queryKey: queryKeys.id(props.id) });
        },
        onSuccess: (_: boolean, variables: { name: string }) => {
            // Update the project to reflect the new promoted tab until it's updated from the server
            const previousProject = queryClient.getQueryData<ViewProject>(queryKeys.id(props.id));
            if (previousProject) {
                queryClient.setQueryData(queryKeys.id(props.id), {
                    ...previousProject,
                    promoted_tabs: [...(previousProject?.promoted_tabs ?? []), variables.name]
                });
            }
        }
    }));
}
