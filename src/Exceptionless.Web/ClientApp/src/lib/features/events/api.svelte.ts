import type { WebSocketMessageValue } from '$features/websockets/models';
import type { CountResult, WorkInProgressResult } from '$shared/models';

import { accessToken } from '$features/auth/index.svelte';
import { queryKeys as stackQueryKeys } from '$features/stacks/api.svelte';
import { DEFAULT_OFFSET } from '$shared/api/api.svelte';
import { type FetchClientResponse, type ProblemDetails, useFetchClient } from '@foundatiofx/fetchclient';
import { createMutation, createQuery, keepPreviousData, QueryClient, useQueryClient } from '@tanstack/svelte-query';
import { SvelteSet } from 'svelte/reactivity';

import type { EventSummaryModel, SummaryTemplateKeys } from './components/summary/index';
import type { PersistentEvent } from './models';

export interface OrganizationEventNotificationRefresher {
    cancel: () => void;
    schedule: (organizationId?: string, refreshImmediately?: boolean, includeStackLists?: boolean) => void;
}

export function createOrganizationEventNotificationRefresher(queryClient: QueryClient): OrganizationEventNotificationRefresher {
    const pendingOrganizationIds = new SvelteSet<string | undefined>();
    let pendingStackListRefresh = false;
    let trailingRefresh: ReturnType<typeof setTimeout> | undefined;
    const refresh = () => {
        const organizationIds = [...pendingOrganizationIds];
        const includeStackLists = pendingStackListRefresh;

        void queryClient.invalidateQueries({
            predicate: (query) =>
                isOrganizationEventDashboardQueryKey(query.queryKey) &&
                (includeStackLists || !isOrganizationStackListQueryKey(query.queryKey)) &&
                (organizationIds.includes(undefined) || organizationIds.includes(query.queryKey[2] as string)),
            queryKey: queryKeys.type,
            refetchType: 'active'
        });
    };

    return {
        cancel: () => {
            pendingOrganizationIds.clear();
            pendingStackListRefresh = false;
            if (trailingRefresh !== undefined) {
                clearTimeout(trailingRefresh);
                trailingRefresh = undefined;
            }
        },
        schedule: (organizationId?: string, refreshImmediately = true, includeStackLists = false) => {
            pendingOrganizationIds.add(organizationId);
            pendingStackListRefresh ||= includeStackLists;
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
                pendingStackListRefresh = false;
            }, ORGANIZATION_EVENT_NOTIFICATION_THROTTLE_MS);
        }
    };
}

export async function invalidatePersistentEventQueries(queryClient: QueryClient, message: WebSocketMessageValue<'PersistentEventChanged'>) {
    const { id, organization_id, project_id, stack_id } = message;
    if (id) {
        await queryClient.invalidateQueries({
            queryKey: queryKeys.id(id)
        });
    }

    if (stack_id) {
        await queryClient.invalidateQueries({
            exact: true,
            queryKey: queryKeys.stacks(stack_id)
        });
    }

    if (project_id) {
        await queryClient.invalidateQueries({
            exact: true,
            queryKey: queryKeys.projects(project_id)
        });
    }

    if (organization_id) {
        await queryClient.invalidateQueries({
            exact: true,
            queryKey: queryKeys.organizations(organization_id)
        });
    }

    if (!id && !stack_id) {
        await queryClient.invalidateQueries({
            predicate: (query) => !isOrganizationEventDashboardQueryKey(query.queryKey),
            queryKey: queryKeys.type
        });
    }
}

export const queryKeys = {
    deleteEvent: (ids: string[] | undefined) => [...queryKeys.type, 'delete', ...(ids ?? [])] as const,
    eventsByReference: (referenceId: string | undefined, projectId?: string | undefined, params?: GetEventsByReferenceRequest['params']) =>
        [...queryKeys.type, 'by-ref', referenceId, projectId, params] as const,
    id: (id: string | undefined) => [...queryKeys.type, id] as const,
    organizations: (id: string | undefined) => [...queryKeys.type, 'organizations', id] as const,
    organizationsCount: (id: string | undefined, params?: GetOrganizationCountRequest['params']) => [...queryKeys.organizations(id), 'count', params] as const,
    organizationsEvents: (id: string | undefined, params?: GetEventsParams) => [...queryKeys.organizations(id), 'events', params] as const,
    projects: (id: string | undefined) => [...queryKeys.type, 'projects', id] as const,
    projectsCount: (id: string | undefined, params?: GetProjectCountRequest['params']) => [...queryKeys.projects(id), 'count', params] as const,
    sessionEvents: (id: string | undefined, projectId?: string | undefined, params?: GetSessionEventsRequest['params']) =>
        [...queryKeys.type, 'sessions', 'session', id, projectId, params] as const,
    sessions: (id: string | undefined) => [...queryKeys.type, 'sessions', 'organizations', id] as const,
    sessionsCount: (id: string | undefined, params?: GetOrganizationSessionsCountRequest['params']) => [...queryKeys.sessions(id), 'count', params] as const,
    stackEvents: (id: string | undefined, params?: GetStackEventsRequest['params']) => [...queryKeys.stacks(id), 'events', params] as const,
    stacks: (id: string | undefined) => [...queryKeys.type, 'stacks', id] as const,
    stacksCount: (id: string | undefined, params?: GetStackCountRequest['params']) => [...queryKeys.stacks(id), 'count', params] as const,
    type: ['PersistentEvent'] as const
};

export const PERSISTENT_EVENT_DELETE_RECONCILE_EVENT = 'PersistentEventDeleteReconcile';
export const PERSISTENT_EVENT_DELETE_RECONCILE_DELAY = 1500;
export const PERSISTENT_EVENT_DELETE_RECONCILE_RETRY_DELAY = 5000;
export const ORGANIZATION_EVENT_QUERY_STALE_TIME_MS = 60 * 1000;
export const ORGANIZATION_EVENT_NOTIFICATION_THROTTLE_MS = 5 * 1000;

export interface DeleteEventsRequest {
    route: {
        ids: string[] | undefined;
    };
}

export interface EventNavigation {
    nextId: null | string;
    previousId: null | string;
}

export interface EventWithNavigation {
    event: PersistentEvent;
    navigation: EventNavigation;
}

export interface GetEventRequest {
    params?: {
        expected_stack_id?: string;
        offset?: string;
        time?: string;
    };
    route: {
        id: string | undefined;
    };
}

export interface GetEventsByReferenceRequest {
    params?: {
        after?: string;
        before?: string;
        limit?: number;
        mode?: 'summary';
        offset?: string;
        page?: number;
    };
    route: {
        projectId?: string | undefined;
        referenceId: string | undefined;
    };
}

export type GetEventsMode = 'stack' | 'summary' | null;

export interface GetEventsParams {
    after?: string;
    before?: string;
    filter?: string;
    include?: 'total';
    limit?: number;
    mode?: GetEventsMode;
    offset?: string;
    page?: number;
    sort?: string;
    time?: string;
}

export interface GetOrganizationCountRequest {
    enabled?: () => boolean;
    params?: {
        aggregations?: string;
        filter?: string;
        mode?: 'stack';
        offset?: string;
        time?: string;
    };
    route: {
        organizationId: string | undefined;
    };
}

export interface GetOrganizationEventsRequest {
    enabled?: () => boolean;
    params?: GetEventsParams;
    route: {
        organizationId: string | undefined;
    };
}

export interface GetOrganizationSessionsCountRequest {
    params?: {
        aggregations?: string;
        filter?: string;
        offset?: string;
        time?: string;
    };
    route: {
        organizationId: string | undefined;
    };
}

export interface GetProjectCountRequest {
    params?: {
        aggregations?: string;
        filter?: string;
        mode?: 'stack';
        offset?: string;
        time?: string;
    };
    route: {
        projectId: string | undefined;
    };
}

export interface GetSessionEventsRequest {
    params?: {
        after?: string;
        before?: string;
        filter?: string;
        limit?: number;
        mode?: 'summary';
        offset?: string;
        sort?: string;
        time?: string;
    };
    route: {
        projectId?: string | undefined;
        sessionId: string | undefined;
    };
}

export interface GetStackCountRequest {
    params?: {
        aggregations?: string;
        filter?: string;
        offset?: string;
        time?: string;
    };
    route: {
        stackId: string | undefined;
    };
}

export interface GetStackEventsRequest {
    enabled?: () => boolean;
    params?: {
        after?: string;
        before?: string;
        filter?: string;
        limit?: number;
        mode?: GetEventsMode;
        offset?: string;
        sort?: string;
        time?: string;
    };
    route: {
        stackId: string | undefined;
    };
}

export function createEventWithNavigationQueryOptions(request: GetEventRequest, queryClient: QueryClient) {
    const eventId = request.route.id;
    const params = request.params
        ? {
              ...request.params
          }
        : undefined;

    return {
        enabled: () => !!accessToken.current && !!eventId,
        placeholderData: keepPreviousData,
        queryFn: async () => {
            const client = useFetchClient();
            const response = await client.getJSON<PersistentEvent>(`events/${eventId}`, {
                params: {
                    ...(DEFAULT_OFFSET
                        ? {
                              offset: DEFAULT_OFFSET
                          }
                        : {}),
                    ...params
                }
            });

            const event = response.data!;
            queryClient.setQueryData(queryKeys.id(eventId), event);

            const previousUrl = response.meta?.links?.previous?.url;
            const nextUrl = response.meta?.links?.next?.url;

            return {
                event,
                navigation: {
                    nextId: nextUrl ? (nextUrl.split('/').pop() ?? null) : null,
                    previousId: previousUrl ? (previousUrl.split('/').pop() ?? null) : null
                }
            };
        },
        queryKey: [...queryKeys.id(eventId), 'withNavigation', params]
    };
}

export function deleteEvent(request: DeleteEventsRequest) {
    const queryClient = useQueryClient();
    return createMutation<WorkInProgressResult, ProblemDetails, void>(() => ({
        enabled: () => !!accessToken.current && !!request.route.ids?.length,
        mutationFn: async () => {
            const client = useFetchClient();
            const response = await client.deleteJSON<WorkInProgressResult>(`events/${request.route.ids?.join(',')}`);

            return response.data!;
        },
        mutationKey: queryKeys.deleteEvent(request.route.ids),
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
            schedulePersistentEventDeleteReconciliation(queryClient);
        }
    }));
}

// Cacheable reads intentionally finish after their observer unmounts so navigation can reuse the result instead of aborting and restarting the request.
export function getEventQuery(request: GetEventRequest) {
    return createQuery<PersistentEvent, ProblemDetails>(() => ({
        enabled: () => !!accessToken.current && !!request.route.id,
        queryFn: async () => {
            const client = useFetchClient();
            const response = await client.getJSON<PersistentEvent>(`events/${request.route.id}`, {
                params: {
                    ...(DEFAULT_OFFSET
                        ? {
                              offset: DEFAULT_OFFSET
                          }
                        : {}),
                    ...request.params
                }
            });

            return response.data!;
        },
        queryKey: queryKeys.id(request.route.id)
    }));
}

export function getEventsByReferenceQuery(request: GetEventsByReferenceRequest) {
    return createQuery<EventSummaryModel<SummaryTemplateKeys>[], ProblemDetails>(() => ({
        enabled: () => !!accessToken.current && !!request.route.referenceId,
        queryFn: async () => {
            const client = useFetchClient();
            const path = request.route.projectId
                ? `projects/${request.route.projectId}/events/by-ref/${encodeURIComponent(request.route.referenceId ?? '')}`
                : `events/by-ref/${encodeURIComponent(request.route.referenceId ?? '')}`;
            const response = await client.getJSON<EventSummaryModel<SummaryTemplateKeys>[]>(path, {
                params: {
                    ...(DEFAULT_OFFSET
                        ? {
                              offset: DEFAULT_OFFSET
                          }
                        : {}),
                    limit: 20,
                    mode: 'summary',
                    page: 1,
                    ...request.params
                }
            });

            return response.data!;
        },
        queryKey: queryKeys.eventsByReference(request.route.referenceId, request.route.projectId, request.params)
    }));
}

export function getEventWithNavigationQuery(request: GetEventRequest) {
    const queryClient = useQueryClient();
    return createQuery<EventWithNavigation, ProblemDetails>(() => createEventWithNavigationQueryOptions(request, queryClient));
}

export function getOrganizationCountQuery(request: GetOrganizationCountRequest) {
    const queryClient = useQueryClient();

    return createQuery<CountResult, ProblemDetails>(() => {
        const organizationId = request.route.organizationId;
        const params = request.params
            ? {
                  ...request.params
              }
            : undefined;

        return {
            enabled: () => !!accessToken.current && !!organizationId && (request.enabled?.() ?? true),
            queryClient,
            queryFn: async () => {
                const client = useFetchClient();
                const response = await client.getJSON<CountResult>(`/organizations/${organizationId}/events/count`, {
                    params: {
                        ...(DEFAULT_OFFSET
                            ? {
                                  offset: DEFAULT_OFFSET
                              }
                            : {}),
                        ...params
                    }
                });

                return response.data!;
            },
            queryKey: queryKeys.organizationsCount(organizationId, params),
            staleTime: ORGANIZATION_EVENT_QUERY_STALE_TIME_MS
        };
    });
}

export function getOrganizationEventsQuery<T = EventSummaryModel<SummaryTemplateKeys>>(request: GetOrganizationEventsRequest) {
    return createQuery<FetchClientResponse<T[]>, ProblemDetails>(() => {
        const organizationId = request.route.organizationId;
        const params = request.params
            ? {
                  ...request.params
              }
            : undefined;

        return {
            enabled: () => !!accessToken.current && !!organizationId && (request.enabled?.() ?? true),
            placeholderData: keepPreviousData,
            queryFn: async () => {
                const client = useFetchClient();
                return await client.getJSON<T[]>(`organizations/${organizationId}/events`, {
                    params: params as Record<string, unknown>
                });
            },
            queryKey: queryKeys.organizationsEvents(organizationId, params),
            staleTime: ORGANIZATION_EVENT_QUERY_STALE_TIME_MS
        };
    });
}

/**
 * Get session count with aggregations for stats and chart data.
 * Uses aggregation: avg:value cardinality:user date:(date^offset cardinality:user)
 */
export function getOrganizationSessionsCountQuery(request: GetOrganizationSessionsCountRequest) {
    const queryClient = useQueryClient();

    return createQuery<CountResult, ProblemDetails>(() => ({
        enabled: () => !!accessToken.current && !!request.route.organizationId,
        queryClient,
        queryFn: async () => {
            const client = useFetchClient();
            const response = await client.getJSON<CountResult>(`/organizations/${request.route.organizationId}/events/count`, {
                params: {
                    ...(DEFAULT_OFFSET
                        ? {
                              offset: DEFAULT_OFFSET
                          }
                        : {}),
                    ...request.params
                }
            });

            return response.data!;
        },
        queryKey: queryKeys.sessionsCount(request.route.organizationId, request.params)
    }));
}

export function getProjectCountQuery(request: GetProjectCountRequest) {
    const queryClient = useQueryClient();

    return createQuery<CountResult, ProblemDetails>(() => {
        const projectId = request.route.projectId;
        const params = request.params
            ? {
                  ...request.params
              }
            : undefined;

        return {
            enabled: () => !!accessToken.current && !!projectId,
            queryClient,
            queryFn: async () => {
                const client = useFetchClient();
                const response = await client.getJSON<CountResult>(`/projects/${projectId}/events/count`, {
                    params: {
                        ...(DEFAULT_OFFSET
                            ? {
                                  offset: DEFAULT_OFFSET
                              }
                            : {}),
                        ...params
                    }
                });

                return response.data!;
            },
            queryKey: queryKeys.projectsCount(projectId, params)
        };
    });
}

/**
 * Get events within a session by session ID.
 * Uses endpoint: /events/sessions/{sessionId}
 */
export function getSessionEventsQuery(request: GetSessionEventsRequest) {
    return createQuery<EventSummaryModel<SummaryTemplateKeys>[], ProblemDetails>(() => ({
        enabled: () => !!accessToken.current && !!request.route.sessionId,
        queryFn: async () => {
            const client = useFetchClient();
            const path = request.route.projectId
                ? `projects/${request.route.projectId}/events/sessions/${request.route.sessionId}`
                : `events/sessions/${request.route.sessionId}`;
            const response = await client.getJSON<EventSummaryModel<SummaryTemplateKeys>[]>(path, {
                params: {
                    ...(DEFAULT_OFFSET
                        ? {
                              offset: DEFAULT_OFFSET
                          }
                        : {}),
                    mode: 'summary',
                    ...request.params
                }
            });

            return response.data!;
        },
        queryKey: queryKeys.sessionEvents(request.route.sessionId, request.route.projectId, request.params)
    }));
}

export function getStackCountQuery(request: GetStackCountRequest) {
    const queryClient = useQueryClient();

    return createQuery<CountResult, ProblemDetails>(() => ({
        enabled: () => !!accessToken.current && !!request.route.stackId,
        queryClient,
        queryFn: async () => {
            const client = useFetchClient();
            const response = await client.getJSON<CountResult>('events/count', {
                params: {
                    ...(DEFAULT_OFFSET
                        ? {
                              offset: DEFAULT_OFFSET
                          }
                        : {}),
                    ...request.params,
                    filter: request.params?.filter?.includes(`stack:${request.route.stackId}`)
                        ? request.params.filter
                        : [request.params?.filter, `stack:${request.route.stackId}`].filter(Boolean).join(' ')
                }
            });

            return response.data!;
        },
        queryKey: queryKeys.stacksCount(request.route.stackId, request.params)
    }));
}

export function getStackEventsQuery(request: GetStackEventsRequest) {
    const queryClient = useQueryClient();

    return createQuery<PersistentEvent[], ProblemDetails>(() => ({
        enabled: () => !!accessToken.current && !!request.route.stackId && (request.enabled?.() ?? true),
        onSuccess: (data: PersistentEvent[]) => {
            data.forEach((event) => {
                queryClient.setQueryData(queryKeys.id(event.id!), event);
            });
        },
        queryClient,
        queryFn: async () => {
            const client = useFetchClient();
            const response = await client.getJSON<PersistentEvent[]>(`stacks/${request.route.stackId}/events`, {
                params: {
                    ...(DEFAULT_OFFSET
                        ? {
                              offset: DEFAULT_OFFSET
                          }
                        : {}),
                    ...request.params
                }
            });

            return response.data!;
        },
        queryKey: queryKeys.stackEvents(request.route.stackId, request.params)
    }));
}

export function schedulePersistentEventDeleteReconciliation(queryClient: QueryClient, eventTarget: EventTarget = document) {
    const invalidateQueryBackedDetails = () =>
        queryClient.invalidateQueries({
            predicate: (query) => !isOrganizationEventsQueryKey(query.queryKey),
            queryKey: queryKeys.type
        });

    eventTarget.dispatchEvent(new Event(PERSISTENT_EVENT_DELETE_RECONCILE_EVENT));
    void queryClient.invalidateQueries({
        queryKey: stackQueryKeys.type
    });
    setTimeout(() => {
        void invalidateQueryBackedDetails();
        void queryClient.invalidateQueries({
            queryKey: stackQueryKeys.type
        });
    }, PERSISTENT_EVENT_DELETE_RECONCILE_DELAY);
    setTimeout(() => {
        eventTarget.dispatchEvent(new Event(PERSISTENT_EVENT_DELETE_RECONCILE_EVENT));
        void invalidateQueryBackedDetails();
        void queryClient.invalidateQueries({
            queryKey: stackQueryKeys.type
        });
    }, PERSISTENT_EVENT_DELETE_RECONCILE_RETRY_DELAY);
}

function isOrganizationEventDashboardQueryKey(queryKey: readonly unknown[]): boolean {
    return queryKey[0] === queryKeys.type[0] && queryKey[1] === 'organizations' && (queryKey[3] === 'count' || queryKey[3] === 'events');
}

function isOrganizationEventsQueryKey(queryKey: readonly unknown[]): boolean {
    return queryKey[0] === queryKeys.type[0] && queryKey[1] === 'organizations' && queryKey[3] === 'events';
}

function isOrganizationStackListQueryKey(queryKey: readonly unknown[]): boolean {
    return isOrganizationEventsQueryKey(queryKey) && (queryKey[4] as GetEventsParams | undefined)?.mode === 'stack';
}
