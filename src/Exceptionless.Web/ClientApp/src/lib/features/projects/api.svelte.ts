import { accessToken } from '$features/auth/index.svelte';
import { type ProblemDetails, useFetchClient } from '@exceptionless/fetchclient';
import { createMutation, createQuery, useQueryClient } from '@tanstack/svelte-query';

import type { ViewProject } from './models';

export const queryKeys = {
    all: ['Project'] as const,
    allWithFilters: (filters: string) => [...queryKeys.all, { filters }] as const,
    id: (id: string | undefined) => [...queryKeys.all, id] as const,
    organization: (id: string | undefined) => [...queryKeys.all, 'organization', id] as const,
    organizationWithFilters: (id: string | undefined, filters: string) => [...queryKeys.organization(id), { filters }] as const
};

export interface GetProjectByIdProps {
    id: string | undefined;
}

export function getProjectByIdQuery(props: GetProjectByIdProps) {
    return createQuery<ViewProject, ProblemDetails>(() => ({
        enabled: () => !!accessToken.value && !!props.id,
        queryFn: async ({ signal }: { signal: AbortSignal }) => {
            const client = useFetchClient();
            const response = await client.getJSON<ViewProject>(`projects/${props.id}`, {
                signal
            });

            if (response.ok) {
                return response.data!;
            }

            throw response.problem;
        },
        queryKey: queryKeys.id(props.id)
    }));
}

export interface GetProjectsByOrganizationIdProps {
    limit?: number;
    organizationId: string | undefined;
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

            if (response.ok) {
                return response.data!;
            }

            throw response.problem;
        },
        queryKey: queryKeys.organization(props.organizationId)
    }));
}

export interface PromoteProjectTabProps {
    id: string;
}

export function mutatePromoteTab(props: PromoteProjectTabProps) {
    const queryClient = useQueryClient();
    return createMutation<boolean, ProblemDetails, { name: string }>(() => ({
        mutationFn: async (params: { name: string }) => {
            const client = useFetchClient();
            const response = await client.post(`projects/${props.id}/promotedtabs`, undefined, {
                params
            });

            if (response.ok) {
                // Update the project to reflect the new promoted tab until it's updated from the server
                const previousProject = queryClient.getQueryData<ViewProject>(queryKeys.id(props.id));
                if (previousProject) {
                    queryClient.setQueryData(queryKeys.id(props.id), {
                        ...previousProject,
                        promoted_tabs: [...(previousProject?.promoted_tabs ?? []), params.name]
                    });
                }

                // TODO: Fix status code returns.
                return true;
            }

            throw response.problem;
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

export interface DemoteProjectTabProps {
    id: string;
}

export function mutateDemoteTab(props: DemoteProjectTabProps) {
    const queryClient = useQueryClient();
    return createMutation<boolean, ProblemDetails, { name: string }>(() => ({
        mutationFn: async (params: { name: string }) => {
            const client = useFetchClient();
            const response = await client.delete(`projects/${props.id}/promotedtabs`, {
                params
            });

            if (response.ok) {
                // TODO: Fix status code returns.
                return true;
            }

            throw response.problem;
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
