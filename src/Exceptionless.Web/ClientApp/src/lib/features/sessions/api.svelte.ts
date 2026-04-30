import type { EventSummaryModel, SummaryTemplateKeys } from '$features/events/components/summary/index';
import type { CountResult } from '$shared/models';

import { accessToken } from '$features/auth/index.svelte';
import { queryKeys as eventQueryKeys } from '$features/events/api.svelte';
import { DEFAULT_OFFSET } from '$shared/api/api.svelte';
import { type ProblemDetails, useFetchClient } from '@exceptionless/fetchclient';
import { createQuery, useQueryClient } from '@tanstack/svelte-query';

export const queryKeys = {
    organizations: (id: string | undefined) => [...eventQueryKeys.type, 'sessions', 'organizations', id] as const,
    organizationsCount: (id: string | undefined, params?: GetOrganizationSessionsCountRequest['params']) =>
        [...queryKeys.organizations(id), 'count', params] as const,
    sessionEvents: (id: string | undefined, params?: GetSessionEventsRequest['params']) => [...eventQueryKeys.type, 'sessions', 'session', id, params] as const
};

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
        enabled: () => !!accessToken.current && !!request.route.organizationId,
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
        queryKey: queryKeys.sessionEvents(request.route.sessionId, request.params)
    }));
}
