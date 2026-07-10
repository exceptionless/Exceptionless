import type {
    NewRateNotificationRule,
    SnoozeRateNotificationRuleRequest,
    UpdateRateNotificationRule,
    ViewRateNotificationRule
} from '$features/rate-notifications/types';

import { accessToken } from '$features/auth/index.svelte';
import { type FetchClientResponse, type ProblemDetails, useFetchClient } from '@exceptionless/fetchclient';
import { createMutation, createQuery, type QueryClient, useQueryClient } from '@tanstack/svelte-query';

const CONSISTENCY_REFRESH_DELAY_MS = 1500;

interface RateNotificationRoute {
    projectId: string | undefined;
    userId: string | undefined;
}

interface RuleMutationVariables<TBody = undefined> {
    body: TBody;
    ruleId: string;
}

export const queryKeys = {
    list: (userId: string | undefined, projectId: string | undefined) => ['RateNotificationRule', userId, projectId] as const
};

export function deleteRateNotificationRule(request: { route: RateNotificationRoute }) {
    const queryClient = useQueryClient();

    return createMutation<void, ProblemDetails, string>(() => ({
        enabled: () => !!accessToken.current && !!request.route.userId && !!request.route.projectId,
        mutationFn: async (ruleId: string) => {
            const client = useFetchClient();
            await client.delete(ruleRoute(request.route, ruleId), { expectedStatusCodes: [204] });
        },
        onSuccess: (_, ruleId) => {
            updateRulesCache(queryClient, request.route, (rules) => rules.filter((rule) => rule.id !== ruleId));
            scheduleConsistencyRefresh(queryClient, request.route);
        }
    }));
}

export function getRateNotificationRulesQuery(request: { params?: { limit?: number; page?: number }; route: RateNotificationRoute }) {
    return createQuery<FetchClientResponse<ViewRateNotificationRule[]>, ProblemDetails>(() => ({
        enabled: () => !!accessToken.current && !!request.route.userId && !!request.route.projectId,
        queryFn: async ({ signal }: { signal: AbortSignal }) => {
            const client = useFetchClient();
            return client.getJSON<ViewRateNotificationRule[]>(ruleRoute(request.route), {
                params: { limit: 50, ...request.params },
                signal
            });
        },
        queryKey: [...queryKeys.list(request.route.userId, request.route.projectId), { params: request.params }]
    }));
}

export function postRateNotificationRule(request: { route: RateNotificationRoute }) {
    const queryClient = useQueryClient();

    return createMutation<ViewRateNotificationRule, ProblemDetails, NewRateNotificationRule>(() => ({
        enabled: () => !!accessToken.current && !!request.route.userId && !!request.route.projectId,
        mutationFn: async (body: NewRateNotificationRule) => {
            const client = useFetchClient();
            const response = await client.postJSON<ViewRateNotificationRule>(ruleRoute(request.route), body);
            return response.data!;
        },
        onSuccess: (rule) => {
            updateRulesCache(queryClient, request.route, (rules) => upsertRule(rules, rule));
            scheduleConsistencyRefresh(queryClient, request.route);
        }
    }));
}

export function postSnoozeRateNotificationRule(request: { route: RateNotificationRoute }) {
    const queryClient = useQueryClient();

    return createMutation<ViewRateNotificationRule, ProblemDetails, RuleMutationVariables<SnoozeRateNotificationRuleRequest>>(() => ({
        enabled: () => !!accessToken.current && !!request.route.userId && !!request.route.projectId,
        mutationFn: async ({ body, ruleId }) => {
            const client = useFetchClient();
            const response = await client.postJSON<ViewRateNotificationRule>(`${ruleRoute(request.route, ruleId)}/snooze`, body, {
                expectedStatusCodes: [200]
            });
            return response.data!;
        },
        onSuccess: (rule) => {
            updateRulesCache(queryClient, request.route, (rules) => upsertRule(rules, rule));
            scheduleConsistencyRefresh(queryClient, request.route);
        }
    }));
}

export function postUnsnoozeRateNotificationRule(request: { route: RateNotificationRoute }) {
    const queryClient = useQueryClient();

    return createMutation<ViewRateNotificationRule, ProblemDetails, string>(() => ({
        enabled: () => !!accessToken.current && !!request.route.userId && !!request.route.projectId,
        mutationFn: async (ruleId: string) => {
            const client = useFetchClient();
            const response = await client.postJSON<ViewRateNotificationRule>(
                `${ruleRoute(request.route, ruleId)}/unsnooze`,
                {},
                {
                    expectedStatusCodes: [200]
                }
            );
            return response.data!;
        },
        onSuccess: (rule) => {
            updateRulesCache(queryClient, request.route, (rules) => upsertRule(rules, rule));
            scheduleConsistencyRefresh(queryClient, request.route);
        }
    }));
}

export function putRateNotificationRule(request: { route: RateNotificationRoute }) {
    const queryClient = useQueryClient();

    return createMutation<ViewRateNotificationRule, ProblemDetails, RuleMutationVariables<UpdateRateNotificationRule>>(() => ({
        enabled: () => !!accessToken.current && !!request.route.userId && !!request.route.projectId,
        mutationFn: async ({ body, ruleId }) => {
            const client = useFetchClient();
            const response = await client.putJSON<ViewRateNotificationRule>(ruleRoute(request.route, ruleId), body);
            return response.data!;
        },
        onSuccess: (rule) => {
            updateRulesCache(queryClient, request.route, (rules) => upsertRule(rules, rule));
            scheduleConsistencyRefresh(queryClient, request.route);
        }
    }));
}

function ruleRoute(route: RateNotificationRoute, ruleId?: string): string {
    const base = `users/${route.userId}/projects/${route.projectId}/rate-notifications`;
    return ruleId ? `${base}/${ruleId}` : base;
}

function scheduleConsistencyRefresh(queryClient: QueryClient, route: RateNotificationRoute): void {
    const queryKey = queryKeys.list(route.userId, route.projectId);
    setTimeout(() => void queryClient.invalidateQueries({ queryKey }), CONSISTENCY_REFRESH_DELAY_MS);
}

function updateRulesCache(queryClient: QueryClient, route: RateNotificationRoute, update: (rules: ViewRateNotificationRule[]) => ViewRateNotificationRule[]) {
    queryClient.setQueriesData<FetchClientResponse<ViewRateNotificationRule[]> | undefined>(
        { queryKey: queryKeys.list(route.userId, route.projectId) },
        (response) => {
            if (!Array.isArray(response?.data)) {
                return response;
            }

            return { ...response, data: update(response.data) };
        }
    );
}

function upsertRule(rules: ViewRateNotificationRule[], rule: ViewRateNotificationRule): ViewRateNotificationRule[] {
    const nextRules = rules.some((existingRule) => existingRule.id === rule.id)
        ? rules.map((existingRule) => (existingRule.id === rule.id ? rule : existingRule))
        : [...rules, rule];

    return nextRules.toSorted((a, b) => a.name.localeCompare(b.name));
}
