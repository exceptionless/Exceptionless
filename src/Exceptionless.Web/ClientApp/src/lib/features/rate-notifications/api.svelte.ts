import type {
    NewRateNotificationRule,
    SnoozeRateNotificationRuleRequest,
    UpdateRateNotificationRule,
    ViewRateNotificationRule
} from '$features/rate-notifications/types';

import { accessToken } from '$features/auth/index.svelte';
import { type FetchClientResponse, type ProblemDetails, useFetchClient } from '@exceptionless/fetchclient';
import { createMutation, createQuery, QueryClient, useQueryClient } from '@tanstack/svelte-query';

export const queryKeys = {
    create: (userId: string | undefined, projectId: string | undefined) =>
        [...queryKeys.list(userId, projectId), 'create'] as const,
    delete: (userId: string | undefined, projectId: string | undefined, ruleId: string | undefined) =>
        [...queryKeys.id(userId, projectId, ruleId), 'delete'] as const,
    id: (userId: string | undefined, projectId: string | undefined, ruleId: string | undefined) =>
        [...queryKeys.list(userId, projectId), ruleId] as const,
    list: (userId: string | undefined, projectId: string | undefined) =>
        ['RateNotificationRule', userId, projectId] as const,
    snooze: (userId: string | undefined, projectId: string | undefined, ruleId: string | undefined) =>
        [...queryKeys.id(userId, projectId, ruleId), 'snooze'] as const,
    unsnooze: (userId: string | undefined, projectId: string | undefined, ruleId: string | undefined) =>
        [...queryKeys.id(userId, projectId, ruleId), 'unsnooze'] as const,
    update: (userId: string | undefined, projectId: string | undefined, ruleId: string | undefined) =>
        [...queryKeys.id(userId, projectId, ruleId), 'update'] as const
};

export async function invalidateRateNotificationQueries(
    queryClient: QueryClient,
    userId: string | undefined,
    projectId: string | undefined,
    ruleId?: string | undefined
) {
    if (ruleId) {
        await queryClient.invalidateQueries({ queryKey: queryKeys.id(userId, projectId, ruleId) });
    }
    await queryClient.invalidateQueries({ queryKey: queryKeys.list(userId, projectId) });
}

function ruleRoute(userId: string, projectId: string, ruleId?: string): string {
    const base = `users/${userId}/projects/${projectId}/rate-notifications`;
    return ruleId ? `${base}/${ruleId}` : base;
}

// ---- List ----

export interface GetRuleListRequest {
    params?: { limit?: number; page?: number };
    route: { projectId: string | undefined; userId: string | undefined };
}

export function getRateNotificationRulesQuery(request: GetRuleListRequest) {
    const queryClient = useQueryClient();

    return createQuery<FetchClientResponse<ViewRateNotificationRule[]>, ProblemDetails>(() => ({
        enabled: () => !!accessToken.current && !!request.route.userId && !!request.route.projectId,
        onSuccess: (data: FetchClientResponse<ViewRateNotificationRule[]>) => {
            data.data?.forEach((rule) => {
                queryClient.setQueryData(queryKeys.id(request.route.userId, request.route.projectId, rule.id), rule);
            });
        },
        queryClient,
        queryFn: async ({ signal }: { signal: AbortSignal }) => {
            const client = useFetchClient();
            const response = await client.getJSON<ViewRateNotificationRule[]>(
                ruleRoute(request.route.userId!, request.route.projectId!),
                { params: { limit: 50, ...request.params }, signal }
            );
            return response;
        },
        queryKey: [...queryKeys.list(request.route.userId, request.route.projectId), { params: request.params }]
    }));
}

// ---- Create ----

export interface CreateRuleRequest {
    route: { projectId: string | undefined; userId: string | undefined };
}

export function createRateNotificationRule(request: CreateRuleRequest) {
    const queryClient = useQueryClient();

    return createMutation<ViewRateNotificationRule, ProblemDetails, NewRateNotificationRule>(() => ({
        enabled: () => !!accessToken.current && !!request.route.userId && !!request.route.projectId,
        mutationFn: async (body: NewRateNotificationRule) => {
            const client = useFetchClient();
            const response = await client.postJSON<ViewRateNotificationRule>(
                ruleRoute(request.route.userId!, request.route.projectId!),
                body
            );
            return response.data!;
        },
        mutationKey: queryKeys.create(request.route.userId, request.route.projectId),
        onSuccess: (rule: ViewRateNotificationRule) => {
            queryClient.setQueryData(queryKeys.id(request.route.userId, request.route.projectId, rule.id), rule);
            queryClient.invalidateQueries({ queryKey: queryKeys.list(request.route.userId, request.route.projectId) });
        }
    }));
}

// ---- Update ----

export interface UpdateRuleRequest {
    route: { projectId: string | undefined; ruleId: string | undefined; userId: string | undefined };
}

export function updateRateNotificationRule(request: UpdateRuleRequest) {
    const queryClient = useQueryClient();

    return createMutation<ViewRateNotificationRule, ProblemDetails, UpdateRateNotificationRule>(() => ({
        enabled: () =>
            !!accessToken.current && !!request.route.userId && !!request.route.projectId && !!request.route.ruleId,
        mutationFn: async (body: UpdateRateNotificationRule) => {
            const client = useFetchClient();
            const response = await client.putJSON<ViewRateNotificationRule>(
                ruleRoute(request.route.userId!, request.route.projectId!, request.route.ruleId!),
                body
            );
            return response.data!;
        },
        mutationKey: queryKeys.update(request.route.userId, request.route.projectId, request.route.ruleId),
        onSuccess: (rule: ViewRateNotificationRule) => {
            queryClient.setQueryData(queryKeys.id(request.route.userId, request.route.projectId, rule.id), rule);
            queryClient.invalidateQueries({ queryKey: queryKeys.list(request.route.userId, request.route.projectId) });
        }
    }));
}

// ---- Delete ----

export interface DeleteRuleRequest {
    route: { projectId: string | undefined; ruleId: string | undefined; userId: string | undefined };
}

export function deleteRateNotificationRule(request: DeleteRuleRequest) {
    const queryClient = useQueryClient();

    return createMutation<void, ProblemDetails, void>(() => ({
        enabled: () =>
            !!accessToken.current && !!request.route.userId && !!request.route.projectId && !!request.route.ruleId,
        mutationFn: async () => {
            const client = useFetchClient();
            await client.delete(ruleRoute(request.route.userId!, request.route.projectId!, request.route.ruleId!), {
                expectedStatusCodes: [204]
            });
        },
        mutationKey: queryKeys.delete(request.route.userId, request.route.projectId, request.route.ruleId),
        onSuccess: () => {
            queryClient.invalidateQueries({ queryKey: queryKeys.list(request.route.userId, request.route.projectId) });
        }
    }));
}

// ---- Snooze ----

export interface SnoozeRuleRequest {
    route: { projectId: string | undefined; ruleId: string | undefined; userId: string | undefined };
}

export function snoozeRateNotificationRule(request: SnoozeRuleRequest) {
    const queryClient = useQueryClient();

    return createMutation<void, ProblemDetails, SnoozeRateNotificationRuleRequest>(() => ({
        enabled: () =>
            !!accessToken.current && !!request.route.userId && !!request.route.projectId && !!request.route.ruleId,
        mutationFn: async (body: SnoozeRateNotificationRuleRequest) => {
            const client = useFetchClient();
            await client.postJSON(
                `${ruleRoute(request.route.userId!, request.route.projectId!, request.route.ruleId!)}/snooze`,
                body,
                { expectedStatusCodes: [200, 204] }
            );
        },
        mutationKey: queryKeys.snooze(request.route.userId, request.route.projectId, request.route.ruleId),
        onSuccess: () => {
            queryClient.invalidateQueries({
                queryKey: queryKeys.id(request.route.userId, request.route.projectId, request.route.ruleId)
            });
            queryClient.invalidateQueries({ queryKey: queryKeys.list(request.route.userId, request.route.projectId) });
        }
    }));
}

// ---- Unsnooze ----

export function unsnoozeRateNotificationRule(request: SnoozeRuleRequest) {
    const queryClient = useQueryClient();

    return createMutation<void, ProblemDetails, void>(() => ({
        enabled: () =>
            !!accessToken.current && !!request.route.userId && !!request.route.projectId && !!request.route.ruleId,
        mutationFn: async () => {
            const client = useFetchClient();
            await client.postJSON(
                `${ruleRoute(request.route.userId!, request.route.projectId!, request.route.ruleId!)}/unsnooze`,
                {},
                { expectedStatusCodes: [200, 204] }
            );
        },
        mutationKey: queryKeys.unsnooze(request.route.userId, request.route.projectId, request.route.ruleId),
        onSuccess: () => {
            queryClient.invalidateQueries({
                queryKey: queryKeys.id(request.route.userId, request.route.projectId, request.route.ruleId)
            });
            queryClient.invalidateQueries({ queryKey: queryKeys.list(request.route.userId, request.route.projectId) });
        }
    }));
}
