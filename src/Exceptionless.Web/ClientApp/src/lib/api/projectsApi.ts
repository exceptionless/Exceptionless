import { createMutation, createQuery, useQueryClient } from '@tanstack/svelte-query';
import { derived, readable, type Readable } from 'svelte/store';
import type { ViewProject } from '$lib/models/api';
import { FetchClient, type FetchClientResponse, type ProblemDetails } from '$api/FetchClient';
import { accessToken } from '$api/auth';

export const queryKeys = {
    all: ['Project'] as const,
    allWithFilters: (filters: string) => [...queryKeys.all, { filters }] as const,
    organization: (id: string | null) => [...queryKeys.all, 'organization', id] as const,
    organizationWithFilters: (id: string | null, filters: string) => [...queryKeys.organization(id), { filters }] as const,
    id: (id: string | null) => [...queryKeys.all, id] as const
};

export function getProjectByIdQuery(id: string | Readable<string | null>) {
    const readableId = typeof id === 'string' || id === null ? readable(id) : id;
    return createQuery<ViewProject, ProblemDetails>(
        derived([accessToken, readableId], ([$accessToken, $id]) => ({
            enabled: !!$accessToken && !!$id,
            queryKey: queryKeys.id($id),
            queryFn: async ({ signal }: { signal: AbortSignal }) => {
                const { getJSON } = new FetchClient();
                const response = await getJSON<ViewProject>(`projects/${$id}`, {
                    signal
                });

                if (response.ok) {
                    return response.data!;
                }

                throw response.problem;
            }
        }))
    );
}

export function getProjectsByOrganizationIdQuery(organizationId: string | Readable<string | null>, limit: number = 1000) {
    const queryClient = useQueryClient();
    const readableOrganizationId = typeof organizationId === 'string' || organizationId === null ? readable(organizationId) : organizationId;
    return createQuery<ViewProject[], ProblemDetails>(
        derived([accessToken, readableOrganizationId], ([$accessToken, $id]) => ({
            enabled: !!$accessToken && !!$id,
            queryClient,
            queryKey: queryKeys.organization($id),
            queryFn: async ({ signal }: { signal: AbortSignal }) => {
                const { getJSON } = new FetchClient();
                const response = await getJSON<ViewProject[]>(`organizations/${$id}/projects`, {
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
        }))
    );
}

export function mutatePromoteTab(id: string) {
    const client = useQueryClient();
    return createMutation<FetchClientResponse<unknown>, ProblemDetails, { name: string }>({
        mutationKey: queryKeys.id(id),
        mutationFn: async (params: { name: string }) => {
            const { post } = new FetchClient();
            const response = await post(`projects/${id}/promotedtabs`, undefined, {
                params
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

export function mutateDemoteTab(id: string) {
    const client = useQueryClient();
    return createMutation<FetchClientResponse<unknown>, ProblemDetails, { name: string }>({
        mutationKey: queryKeys.id(id),
        mutationFn: async ({ name }) => {
            const { remove } = new FetchClient();
            const response = await remove(`projects/${id}/promotedtabs`, {
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
