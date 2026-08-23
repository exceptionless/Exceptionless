import type { StackSummaryModel, SummaryTemplateKeys } from '$features/events/components/summary';
import type { WebSocketMessageValue } from '$features/websockets/models';
import type { WorkInProgressResult } from '$shared/models';

import { accessToken } from '$features/auth/index.svelte';
import { type FetchClientResponse, type ProblemDetails, useFetchClient } from '@foundatiofx/fetchclient';
import { createMutation, createQuery, QueryClient, useQueryClient } from '@tanstack/svelte-query';
import { SvelteSet } from 'svelte/reactivity';

import type { Stack, StackStatus } from './models';

export interface StackNotificationRefresher {
    cancel: () => void;
    schedule: (organizationId?: string, projectId?: string, refreshImmediately?: boolean) => void;
}

export function createStackNotificationRefresher(queryClient: QueryClient): StackNotificationRefresher {
    const pendingOrganizationIds = new SvelteSet<string | undefined>();
    const pendingProjectIds = new SvelteSet<string | undefined>();
    let trailingRefresh: ReturnType<typeof setTimeout> | undefined;
    const refresh = () => {
        const organizationIds = [...pendingOrganizationIds];
        const projectIds = [...pendingProjectIds];

        void queryClient.invalidateQueries({
            predicate: (query) =>
                (isOrganizationStackRollupsQueryKey(query.queryKey) &&
                    (organizationIds.includes(undefined) || organizationIds.includes(query.queryKey[3] as string))) ||
                (isProjectStacksQueryKey(query.queryKey) && (projectIds.includes(undefined) || projectIds.includes(query.queryKey[2] as string))),
            queryKey: queryKeys.type,
            refetchType: 'active'
        });
    };

    return {
        cancel: () => {
            pendingOrganizationIds.clear();
            pendingProjectIds.clear();
            if (trailingRefresh !== undefined) {
                clearTimeout(trailingRefresh);
                trailingRefresh = undefined;
            }
        },
        schedule: (organizationId?: string, projectId?: string, refreshImmediately = true) => {
            pendingOrganizationIds.add(organizationId);
            pendingProjectIds.add(projectId);
            if (trailingRefresh !== undefined) {
                return;
            }

            if (refreshImmediately) {
                refresh();
            }

            trailingRefresh = setTimeout(() => {
                trailingRefresh = undefined;
                refresh();
                pendingOrganizationIds.clear();
                pendingProjectIds.clear();
            }, STACK_NOTIFICATION_THROTTLE_MS);
        }
    };
}

export async function invalidateStackQueries(queryClient: QueryClient, message: WebSocketMessageValue<'StackChanged'>) {
    const { id } = message;
    if (id) {
        await queryClient.invalidateQueries({
            queryKey: queryKeys.id(id)
        });
    } else {
        await queryClient.invalidateQueries({
            predicate: (query) => !isProjectStacksQueryKey(query.queryKey),
            queryKey: queryKeys.type
        });
    }
}

export const STACK_LIST_QUERY_STALE_TIME_MS = 60 * 1000;
export const STACK_NOTIFICATION_THROTTLE_MS = 5 * 1000;

export const queryKeys = {
    deleteMarkCritical: (ids: string[] | undefined) => [...queryKeys.ids(ids), 'mark-not-critical'] as const,
    deleteStack: (ids: string[] | undefined) => [...queryKeys.ids(ids), 'delete'] as const,
    id: (id: string | undefined) => [...queryKeys.type, id] as const,
    ids: (ids: string[] | undefined) => [...queryKeys.type, ...(ids ?? [])] as const,
    organizationRollups: (organizationId: string | undefined, params?: GetStackRollupsParams) =>
        [...queryKeys.type, 'rollups', 'organization', organizationId, params] as const,
    organizationRollupsStats: (organizationId: string | undefined, params?: GetStackRollupStatsParams) =>
        [...queryKeys.type, 'rollups', 'organization', organizationId, 'stats', params] as const,
    postAddLink: (id: string | undefined) => [...queryKeys.id(id), 'add-link'] as const,
    postChangeStatus: (ids: string[] | undefined) => [...queryKeys.ids(ids), 'change-status'] as const,
    postMarkCritical: (ids: string[] | undefined) => [...queryKeys.ids(ids), 'mark-critical'] as const,
    postMarkFixed: (ids: string[] | undefined) => [...queryKeys.ids(ids), 'mark-fixed'] as const,
    postMarkSnoozed: (ids: string[] | undefined) => [...queryKeys.ids(ids), 'mark-snoozed'] as const,
    postPromote: (ids: string[] | undefined) => [...queryKeys.ids(ids), 'promote'] as const,
    postRemoveLink: (id: string | undefined) => [...queryKeys.id(id), 'remove-link'] as const,
    project: (projectId: string | undefined, params?: GetProjectStacksParams) =>
        [
            ...queryKeys.projects(projectId),
            {
                params
            }
        ] as const,
    projects: (projectId: string | undefined) => [...queryKeys.type, 'project', projectId] as const,
    type: ['Stack'] as const
};

export interface DeleteStackRequest {
    route: {
        ids: string[] | undefined;
    };
}

export interface GetOrganizationStackRollupsRequest {
    enabled?: () => boolean;
    params?: GetStackRollupsParams;
    route: {
        organizationId: string | undefined;
    };
}

export interface GetOrganizationStackRollupStatsRequest {
    enabled?: () => boolean;
    params?: GetStackRollupStatsParams;
    route: {
        organizationId: string | undefined;
    };
}

export interface GetProjectStacksParams {
    after?: string;
    before?: string;
    filter?: string;
    limit?: number;
    sort?: string;
}

export interface GetProjectStacksRequest {
    params?: GetProjectStacksParams;
    route: {
        projectId: string | undefined;
    };
}

export interface GetStackRequest {
    route: {
        id: string | undefined;
    };
}

export interface GetStackRollupsParams {
    after?: string;
    before?: string;
    filter?: string;
    include?: 'total';
    limit?: number;
    offset?: string;
    sort?: '-first_occurrence' | '-last_occurrence' | '-total' | '-users' | 'first_occurrence' | 'last_occurrence' | 'total' | 'users';
    time?: string;
}

export interface GetStackRollupStatsParams {
    filter?: string;
    offset?: string;
    time?: string;
}

export interface PostAddLinkRequest {
    route: {
        id: string | undefined;
    };
}

export interface PostChangeStatusRequest {
    route: {
        ids: string[] | undefined;
    };
}

export interface PostMarkCriticalRequest {
    route: {
        ids: string[] | undefined;
    };
}

export interface PostMarkFixedRequest {
    route: {
        ids: string[] | undefined;
    };
}

export interface PostMarkSnoozedRequest {
    route: {
        ids: string[] | undefined;
    };
}

export interface PostPromoteRequest {
    route: {
        ids: string[] | undefined;
    };
}

export interface PostRemoveLinkRequest {
    route: {
        id: string | undefined;
    };
}

export interface StackRollupStats {
    buckets: StackRollupStatsBucket[];
    new_stacks: number;
    total_events: number;
    total_stacks: number;
}

export interface StackRollupStatsBucket {
    date: string;
    events: number;
    stacks: number;
}

export function deleteMarkCritical(request: PostMarkCriticalRequest) {
    const queryClient = useQueryClient();
    return createMutation<void, ProblemDetails, void>(() => ({
        enabled: () => !!accessToken.current && !!request.route.ids?.length,
        mutationFn: async () => {
            const client = useFetchClient();
            await client.delete(`stacks/${request.route.ids?.join(',')}/mark-critical`);
        },
        mutationKey: queryKeys.deleteMarkCritical(request.route.ids),
        onError: () => {
            request.route.ids?.forEach((id) =>
                queryClient.invalidateQueries({
                    queryKey: queryKeys.id(id)
                })
            );
        },
        onSuccess: () => {
            request.route.ids?.forEach((id) =>
                queryClient.invalidateQueries({
                    queryKey: queryKeys.id(id)
                })
            );
        }
    }));
}

export function deleteStack(request: DeleteStackRequest) {
    const queryClient = useQueryClient();
    return createMutation<WorkInProgressResult, ProblemDetails, void>(() => ({
        enabled: () => !!accessToken.current && !!request.route.ids?.length,
        mutationFn: async () => {
            const client = useFetchClient();
            const response = await client.deleteJSON<WorkInProgressResult>(`stacks/${request.route.ids?.join(',')}`);

            return response.data!;
        },
        mutationKey: queryKeys.deleteStack(request.route.ids),
        onError: () => {
            request.route.ids?.forEach((id) =>
                queryClient.invalidateQueries({
                    queryKey: queryKeys.id(id)
                })
            );
        },
        onSuccess: () => {
            request.route.ids?.forEach((id) =>
                queryClient.invalidateQueries({
                    queryKey: queryKeys.id(id)
                })
            );
        }
    }));
}

export function getOrganizationStackRollupsQuery(request: GetOrganizationStackRollupsRequest) {
    return createQuery<FetchClientResponse<StackSummaryModel<SummaryTemplateKeys>[]>, ProblemDetails>(() => ({
        enabled: () => !!accessToken.current && !!request.route.organizationId && (request.enabled?.() ?? true),
        queryFn: async () => {
            const client = useFetchClient();
            return await client.getJSON<StackSummaryModel<SummaryTemplateKeys>[]>(`organizations/${request.route.organizationId}/stack-rollups`, {
                params: request.params as Record<string, unknown>
            });
        },
        queryKey: queryKeys.organizationRollups(request.route.organizationId, request.params),
        staleTime: STACK_LIST_QUERY_STALE_TIME_MS
    }));
}

export function getOrganizationStackRollupStatsQuery(request: GetOrganizationStackRollupStatsRequest) {
    return createQuery<StackRollupStats, ProblemDetails>(() => ({
        enabled: () => !!accessToken.current && !!request.route.organizationId && (request.enabled?.() ?? true),
        queryFn: async () => {
            const client = useFetchClient();
            const response = await client.getJSON<StackRollupStats>(`organizations/${request.route.organizationId}/stack-rollups/stats`, {
                params: request.params as Record<string, unknown>
            });
            return response.data!;
        },
        queryKey: queryKeys.organizationRollupsStats(request.route.organizationId, request.params),
        staleTime: STACK_LIST_QUERY_STALE_TIME_MS
    }));
}

// Cacheable reads intentionally finish after their observer unmounts so navigation can reuse the result instead of aborting and restarting the request.
export function getProjectStacksQuery(request: GetProjectStacksRequest) {
    const queryClient = useQueryClient();

    return createQuery<FetchClientResponse<Stack[]>, ProblemDetails>(() => ({
        enabled: () => !!accessToken.current && !!request.route.projectId,
        onSuccess: (data: FetchClientResponse<Stack[]>) => {
            data.data?.forEach((stack) => {
                queryClient.setQueryData(queryKeys.id(stack.id!), stack);
            });
        },
        queryClient,
        queryFn: async () => {
            const client = useFetchClient();
            const response = await client.getJSON<Stack[]>(`projects/${request.route.projectId}/stacks`, {
                params: request.params as Record<string, unknown>
            });

            return response;
        },
        queryKey: queryKeys.project(request.route.projectId, request.params),
        staleTime: STACK_LIST_QUERY_STALE_TIME_MS
    }));
}

export function getStackQuery(request: GetStackRequest) {
    return createQuery<Stack, ProblemDetails>(() => ({
        enabled: () => !!accessToken.current && !!request.route.id,
        queryFn: async () => {
            const client = useFetchClient();
            const response = await client.getJSON<Stack>(`stacks/${request.route.id}`);

            return response.data!;
        },
        queryKey: queryKeys.id(request.route.id)
    }));
}

export function postAddLink(request: PostAddLinkRequest) {
    const queryClient = useQueryClient();
    return createMutation<void, ProblemDetails, string>(() => ({
        enabled: () => !!accessToken.current && !!request.route.id,
        mutationFn: async (url: string) => {
            const client = useFetchClient();
            await client.post(`stacks/${request.route.id}/add-link`, {
                value: url
            });
        },
        mutationKey: queryKeys.postAddLink(request.route.id),
        onError: () => {
            queryClient.invalidateQueries({
                queryKey: queryKeys.id(request.route.id)
            });
        },
        onSuccess: () => {
            queryClient.invalidateQueries({
                queryKey: queryKeys.id(request.route.id)
            });
        }
    }));
}

export function postChangeStatus(request: PostChangeStatusRequest) {
    const queryClient = useQueryClient();
    return createMutation<void, ProblemDetails, StackStatus>(() => ({
        enabled: () => !!accessToken.current && !!request.route.ids?.length,
        mutationFn: async (status: StackStatus) => {
            const client = useFetchClient();
            await client.post(`stacks/${request.route.ids?.join(',')}/change-status`, undefined, {
                params: {
                    status
                }
            });
        },
        mutationKey: queryKeys.postChangeStatus(request.route.ids),
        onError: () => {
            request.route.ids?.forEach((id) =>
                queryClient.invalidateQueries({
                    queryKey: queryKeys.id(id)
                })
            );
        },
        onSuccess: () => {
            request.route.ids?.forEach((id) =>
                queryClient.invalidateQueries({
                    queryKey: queryKeys.id(id)
                })
            );
        }
    }));
}

export function postMarkCritical(request: PostMarkCriticalRequest) {
    const queryClient = useQueryClient();
    return createMutation<void, ProblemDetails, void>(() => ({
        enabled: () => !!accessToken.current && !!request.route.ids?.length,
        mutationFn: async () => {
            const client = useFetchClient();
            await client.post(`stacks/${request.route.ids?.join(',')}/mark-critical`);
        },
        mutationKey: queryKeys.postMarkCritical(request.route.ids),
        onError: () => {
            request.route.ids?.forEach((id) =>
                queryClient.invalidateQueries({
                    queryKey: queryKeys.id(id)
                })
            );
        },
        onSuccess: () => {
            request.route.ids?.forEach((id) =>
                queryClient.invalidateQueries({
                    queryKey: queryKeys.id(id)
                })
            );
        }
    }));
}

export function postMarkFixed(request: PostMarkFixedRequest) {
    const queryClient = useQueryClient();
    return createMutation<void, ProblemDetails, string | undefined>(() => ({
        enabled: () => !!accessToken.current && !!request.route.ids?.length,
        mutationFn: async (version?: string) => {
            const client = useFetchClient();
            await client.post(`stacks/${request.route.ids?.join(',')}/mark-fixed`, undefined, {
                params: {
                    version
                }
            });
        },
        mutationKey: queryKeys.postMarkFixed(request.route.ids),
        onError: () => {
            request.route.ids?.forEach((id) =>
                queryClient.invalidateQueries({
                    queryKey: queryKeys.id(id)
                })
            );
        },
        onSuccess: () => {
            request.route.ids?.forEach((id) =>
                queryClient.invalidateQueries({
                    queryKey: queryKeys.id(id)
                })
            );
        }
    }));
}

export function postMarkSnoozed(request: PostMarkSnoozedRequest) {
    const queryClient = useQueryClient();
    return createMutation<void, ProblemDetails, Date>(() => ({
        enabled: () => !!accessToken.current && !!request.route.ids?.length,
        mutationFn: async (snoozeUntilUtc: Date) => {
            const client = useFetchClient();
            await client.post(`stacks/${request.route.ids?.join(',')}/mark-snoozed`, undefined, {
                params: {
                    snoozeUntilUtc: snoozeUntilUtc.toISOString()
                }
            });
        },
        mutationKey: queryKeys.postMarkSnoozed(request.route.ids),
        onError: () => {
            request.route.ids?.forEach((id) =>
                queryClient.invalidateQueries({
                    queryKey: queryKeys.id(id)
                })
            );
        },
        onSuccess: () => {
            request.route.ids?.forEach((id) =>
                queryClient.invalidateQueries({
                    queryKey: queryKeys.id(id)
                })
            );
        }
    }));
}

export function postPromote(request: PostPromoteRequest) {
    const queryClient = useQueryClient();
    return createMutation<FetchClientResponse<unknown>, ProblemDetails, void>(() => ({
        enabled: () => !!accessToken.current && !!request.route.ids?.length,
        mutationFn: async () => {
            const client = useFetchClient();
            const response = await client.post(`stacks/${request.route.ids?.join(',')}/promote`, undefined, {
                expectedStatusCodes: [200, 404, 426, 501]
            });

            return response;
        },
        mutationKey: queryKeys.postPromote(request.route.ids),
        onError: () => {
            request.route.ids?.forEach((id) =>
                queryClient.invalidateQueries({
                    queryKey: queryKeys.id(id)
                })
            );
        },
        onSuccess: () => {
            request.route.ids?.forEach((id) =>
                queryClient.invalidateQueries({
                    queryKey: queryKeys.id(id)
                })
            );
        }
    }));
}

export function postRemoveLink(request: PostRemoveLinkRequest) {
    const queryClient = useQueryClient();
    return createMutation<void, ProblemDetails, string>(() => ({
        enabled: () => !!accessToken.current && !!request.route.id,
        mutationFn: async (url: string) => {
            const client = useFetchClient();
            await client.post(`stacks/${request.route.id}/remove-link`, {
                value: url
            });
        },
        mutationKey: queryKeys.postRemoveLink(request.route.id),
        onError: () => {
            queryClient.invalidateQueries({
                queryKey: queryKeys.id(request.route.id)
            });
        },
        onSuccess: () => {
            queryClient.invalidateQueries({
                queryKey: queryKeys.id(request.route.id)
            });
        }
    }));
}

export async function prefetchStack(request: GetStackRequest) {
    if (!accessToken.current) {
        return;
    }

    const queryClient = useQueryClient();
    await queryClient.prefetchQuery<Stack, ProblemDetails>({
        queryFn: async () => {
            const client = useFetchClient();
            const response = await client.getJSON<Stack>(`stacks/${request.route.id}`);

            return response.data!;
        },
        queryKey: queryKeys.id(request.route.id)
    });
}

function isOrganizationStackRollupsQueryKey(queryKey: readonly unknown[]): boolean {
    return queryKey[0] === queryKeys.type[0] && queryKey[1] === 'rollups' && queryKey[2] === 'organization';
}

function isProjectStacksQueryKey(queryKey: readonly unknown[]): boolean {
    return queryKey[0] === queryKeys.type[0] && queryKey[1] === 'project';
}
