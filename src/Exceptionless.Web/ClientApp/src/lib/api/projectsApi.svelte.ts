import { createMutation, createQuery, useQueryClient } from '@tanstack/svelte-query-runes';
import type { ViewProject } from '$lib/models/api';
import { useFetchClient, type FetchClientResponse, type ProblemDetails } from '@exceptionless/fetchclient';
import { accessToken } from '$api/auth.svelte';

export const queryKeys = {
    all: ['Project'] as const,
    allWithFilters: (filters: string) => [...queryKeys.all, { filters }] as const,
    organization: (id: string | null) => [...queryKeys.all, 'organization', id] as const,
    organizationWithFilters: (id: string | null, filters: string) => [...queryKeys.organization(id), { filters }] as const,
    id: (id: string | null) => [...queryKeys.all, id] as const
};

export function getProjectByIdQuery(id: string) {
    const queryOptions = $derived({
        enabled: !!accessToken.value && !!id,
        queryKey: queryKeys.id(id),
        queryFn: async ({ signal }: { signal: AbortSignal }) => {
            const client = useFetchClient();
            const response = await client.getJSON<ViewProject>(`projects/${id}`, {
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

export function getProjectsByOrganizationIdQuery(organizationId: string, limit: number = 1000) {
    const queryClient = useQueryClient();
    const queryOptions = $derived({
        enabled: !!accessToken.value && !!organizationId,
        queryClient,
        queryKey: queryKeys.organization(organizationId),
        queryFn: async ({ signal }: { signal: AbortSignal }) => {
            const client = useFetchClient();
            const response = await client.getJSON<ViewProject[]>(`organizations/${organizationId}/projects`, {
                signal,
                params: {
                    limit
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

export function mutatePromoteTab(id: string) {
    const queryClient = useQueryClient();
    return createMutation<FetchClientResponse<unknown>, ProblemDetails, { name: string }>({
        mutationKey: queryKeys.id(id),
        mutationFn: async (params: { name: string }) => {
            const client = useFetchClient();
            const response = await client.post(`projects/${id}/promotedtabs`, undefined, {
                params
            });

            if (response.ok) {
                return response;
            }

            throw response.problem;
        },
        onSettled: () => {
            queryClient.invalidateQueries({ queryKey: queryKeys.id(id) });
        }
    });
}

export function mutateDemoteTab(id: string) {
    const client = useQueryClient();
    return createMutation<FetchClientResponse<unknown>, ProblemDetails, { name: string }>({
        mutationKey: queryKeys.id(id),
        mutationFn: async ({ name }) => {
            const client = useFetchClient();
            const response = await client.delete(`projects/${id}/promotedtabs`, {
                params: { name }
            });

            if (response.ok) {
                return response;
            }

            throw response.problem;
        },
        onSettled: () => {
            client.invalidateQueries({ queryKey: queryKeys.id(id) });
        }
    });
}
