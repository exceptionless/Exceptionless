import { createMutation, createQuery, useQueryClient } from '@tanstack/svelte-query';
import type { ViewProject } from '$lib/models/api';
import { useFetchClient, type FetchClientResponse, type ProblemDetails } from '@exceptionless/fetchclient';
import { accessToken } from '$api/auth.svelte';

export const queryKeys = {
    all: ['Project'] as const,
    allWithFilters: (filters: string) => [...queryKeys.all, { filters }] as const,
    organization: (id: string | undefined) => [...queryKeys.all, 'organization', id] as const,
    organizationWithFilters: (id: string | undefined, filters: string) => [...queryKeys.organization(id), { filters }] as const,
    id: (id: string | undefined) => [...queryKeys.all, id] as const
};

export interface GetProjectByIdProps {
    id: string | undefined;
}

export function getProjectByIdQuery(props: GetProjectByIdProps) {
    const queryOptions = $derived({
        enabled: !!accessToken.value && !!props.id,
        queryKey: queryKeys.id(props.id),
        queryFn: async ({ signal }: { signal: AbortSignal }) => {
            const client = useFetchClient();
            const response = await client.getJSON<ViewProject>(`projects/${props.id}`, {
                signal
            });

            if (response.ok) {
                return response.data!;
            }

            throw response.problem;
        }
    });

    return createQuery<ViewProject, ProblemDetails>(queryOptions);
}

export interface GetProjectsByOrganizationIdProps {
    organizationId: string | undefined;
    limit?: number;
}

export function getProjectsByOrganizationIdQuery(props: GetProjectsByOrganizationIdProps) {
    const queryClient = useQueryClient();
    const queryOptions = $derived({
        enabled: !!accessToken.value && !!props.organizationId,
        queryClient,
        queryKey: queryKeys.organization(props.organizationId),
        queryFn: async ({ signal }: { signal: AbortSignal }) => {
            const client = useFetchClient();
            const response = await client.getJSON<ViewProject[]>(`organizations/${props.organizationId}/projects`, {
                signal,
                params: {
                    limit: props.limit ?? 1000
                }
            });

            if (response.ok) {
                response.data?.forEach((project) => {
                    queryClient.setQueryData(queryKeys.id(project.id!), project);
                });

                return response.data!;
            }

            throw response.problem;
        }
    });

    return createQuery<ViewProject[], ProblemDetails>(queryOptions);
}

export interface PromoteProjectTabProps {
    id: string;
}

export function mutatePromoteTab(props: PromoteProjectTabProps) {
    const queryClient = useQueryClient();
    return createMutation<FetchClientResponse<unknown>, ProblemDetails, { name: string }>({
        mutationKey: queryKeys.id(props.id),
        mutationFn: async (params: { name: string }) => {
            const client = useFetchClient();
            const response = await client.post(`projects/${props.id}/promotedtabs`, undefined, {
                params
            });

            if (response.ok) {
                return response;
            }

            throw response.problem;
        },
        onSettled: () => {
            queryClient.invalidateQueries({ queryKey: queryKeys.id(props.id) });
        }
    });
}

export interface DemoteProjectTabProps {
    id: string;
}

export function mutateDemoteTab(props: DemoteProjectTabProps) {
    const client = useQueryClient();
    return createMutation<FetchClientResponse<unknown>, ProblemDetails, { name: string }>({
        mutationKey: queryKeys.id(props.id),
        mutationFn: async ({ name }) => {
            const client = useFetchClient();
            const response = await client.delete(`projects/${props.id}/promotedtabs`, {
                params: { name }
            });

            if (response.ok) {
                return response;
            }

            throw response.problem;
        },
        onSettled: () => {
            client.invalidateQueries({ queryKey: queryKeys.id(props.id) });
        }
    });
}
