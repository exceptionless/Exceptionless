import type { CountResult } from '$shared/models';

import { accessToken } from '$features/auth/index.svelte';
import { DEFAULT_OFFSET } from '$shared/api/api.svelte';
import { type ProblemDetails, useFetchClient } from '@exceptionless/fetchclient';
import { createQuery, useQueryClient } from '@tanstack/svelte-query';

import type { EventSummaryModel, SummaryTemplateKeys } from '$features/events/components/summary/index';

export const queryKeys = {
    organizations: (id: string | undefined) => [...queryKeys.type, 'organizations', id] as const,
    organizationsCount: (id: string | undefined, params?: GetOrganizationSessionsCountRequest['params']) =>
        [...queryKeys.organizations(id), 'count', params] as const,
    projects: (id: string | undefined) => [...queryKeys.type, 'projects', id] as const,
    projectsCount: (id: string | undefined, params?: GetProjectSessionsCountRequest['params']) => [...queryKeys.projects(id), 'count', params] as const,
    sessionEvents: (id: string | undefined) => [...queryKeys.type, 'session', id] as const,
    type: ['Session'] as const
};

export interface GetSessionsParams {
    after?: string;
    before?: string;
    filter?: string;
    limit?: number;
    mode?: 'summary';
    offset?: string;
    page?: number;
    sort?: string;
    time?: string;
}

export interface GetOrganizationSessionsCountRequest {
    enabled?: () => boolean;
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

export interface GetProjectSessionsCountRequest {
    params?: {
        aggregations?: string;
        filter?: string;
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
        sessionId: string | undefined;
    };
}

/**
 * Get session count with aggregations for stats and chart data.
 * Uses aggregation: avg:value cardinality:user date:(date^offset cardinality:user)
 */
export function getOrganizationSessionsCountQuery(request: GetOrganizationSessionsCountRequest) {
    const queryClient = useQueryClient();

    return createQuery<CountResult, ProblemDetails>(() => ({
        enabled: () => !!accessToken.current && !!request.route.organizationId && (request.enabled?.() ?? true),
        queryClient,
        queryFn: async ({ signal }: { signal: AbortSignal }) => {
            const client = useFetchClient();
            const response = await client.getJSON<CountResult>(`/organizations/${request.route.organizationId}/events/count`, {
                params: {
                    ...(DEFAULT_OFFSET ? { offset: DEFAULT_OFFSET } : {}),
                    ...request.params
                },
                signal
            });

            return response.data!;
        },
        queryKey: queryKeys.organizationsCount(request.route.organizationId, request.params)
    }));
}

/**
 * Get events within a session by session ID.
 * Uses endpoint: /events/sessions/{sessionId}
 */
export function getSessionEventsQuery(request: GetSessionEventsRequest) {
    return createQuery<EventSummaryModel<SummaryTemplateKeys>[], ProblemDetails>(() => ({
        enabled: () => !!accessToken.current && !!request.route.sessionId,
        queryFn: async ({ signal }: { signal: AbortSignal }) => {
            const client = useFetchClient();
            const response = await client.getJSON<EventSummaryModel<SummaryTemplateKeys>[]>(`events/sessions/${request.route.sessionId}`, {
                params: {
                    ...(DEFAULT_OFFSET ? { offset: DEFAULT_OFFSET } : {}),
                    mode: 'summary',
                    ...request.params
                },
                signal
            });

            return response.data!;
        },
        queryKey: queryKeys.sessionEvents(request.route.sessionId)
    }));
}
