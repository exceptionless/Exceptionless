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

interface BodyMutationVariables<TBody> extends MutationRoute {
    body: TBody;
}

interface MutationRoute {
    projectId: string;
    userId: string;
}

interface RateNotificationRoute {
    projectId: string | undefined;
    userId: string | undefined;
}

interface RuleBodyMutationVariables<TBody> extends RuleMutationVariables {
    body: TBody;
}

interface RuleMutationVariables extends MutationRoute {
    ruleId: string;
}

export const queryKeys = {
    list: (userId: string | undefined, projectId: string | undefined) => ['RateNotificationRule', userId, projectId] as const
};

export function deleteRateNotificationRule() {
    const queryClient = useQueryClient();

    return createMutation<void, ProblemDetails, RuleMutationVariables>(() => ({
        mutationFn: async (variables) => {
            const client = useFetchClient();
            await client.delete(ruleRoute(variables, variables.ruleId), { expectedStatusCodes: [204] });
        },
        onSuccess: (_, variables) => {
            updateRulesCache(queryClient, variables, (rules) => rules.filter((rule) => rule.id !== variables.ruleId));
            scheduleConsistencyRefresh(queryClient, variables);
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

export function postRateNotificationRule() {
    const queryClient = useQueryClient();

    return createMutation<ViewRateNotificationRule, ProblemDetails, BodyMutationVariables<NewRateNotificationRule>>(() => ({
        mutationFn: async ({ body, ...route }) => {
            const client = useFetchClient();
            const response = await client.postJSON<ViewRateNotificationRule>(ruleRoute(route), body);
            return response.data!;
        },
        onSuccess: (rule, variables) => {
            updateRulesCache(queryClient, variables, (rules) => upsertRule(rules, rule));
            scheduleConsistencyRefresh(queryClient, variables);
        }
    }));
}

export function postSnoozeRateNotificationRule() {
    const queryClient = useQueryClient();

    return createMutation<ViewRateNotificationRule, ProblemDetails, RuleBodyMutationVariables<SnoozeRateNotificationRuleRequest>>(() => ({
        mutationFn: async ({ body, ruleId, ...route }) => {
            const client = useFetchClient();
            const response = await client.postJSON<ViewRateNotificationRule>(`${ruleRoute(route, ruleId)}/snooze`, body, {
                expectedStatusCodes: [200]
            });
            return response.data!;
        },
        onSuccess: (rule, variables) => {
            updateRulesCache(queryClient, variables, (rules) => upsertRule(rules, rule));
            scheduleConsistencyRefresh(queryClient, variables);
        }
    }));
}

export function postUnsnoozeRateNotificationRule() {
    const queryClient = useQueryClient();

    return createMutation<ViewRateNotificationRule, ProblemDetails, RuleMutationVariables>(() => ({
        mutationFn: async (variables) => {
            const client = useFetchClient();
            const response = await client.postJSON<ViewRateNotificationRule>(
                `${ruleRoute(variables, variables.ruleId)}/unsnooze`,
                {},
                {
                    expectedStatusCodes: [200]
                }
            );
            return response.data!;
        },
        onSuccess: (rule, variables) => {
            updateRulesCache(queryClient, variables, (rules) => upsertRule(rules, rule));
            scheduleConsistencyRefresh(queryClient, variables);
        }
    }));
}

export function putRateNotificationRule() {
    const queryClient = useQueryClient();

    return createMutation<ViewRateNotificationRule, ProblemDetails, RuleBodyMutationVariables<UpdateRateNotificationRule>>(() => ({
        mutationFn: async ({ body, ruleId, ...route }) => {
            const client = useFetchClient();
            const response = await client.putJSON<ViewRateNotificationRule>(ruleRoute(route, ruleId), body);
            return response.data!;
        },
        onSuccess: (rule, variables) => {
            updateRulesCache(queryClient, variables, (rules) => upsertRule(rules, rule));
            scheduleConsistencyRefresh(queryClient, variables);
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
