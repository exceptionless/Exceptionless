import type { ViewRateNotificationRule } from '$generated/api';
import type { FetchClientResponse } from '@exceptionless/fetchclient';

import { RateNotificationSignal, RateNotificationSubject } from '$generated/api';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';

const mocks = vi.hoisted(() => ({
    cancelQueries: vi.fn(),
    client: {
        delete: vi.fn(),
        getJSON: vi.fn(),
        postJSON: vi.fn(),
        putJSON: vi.fn()
    },
    getQueriesData: vi.fn(),
    invalidateQueries: vi.fn(),
    setQueriesData: vi.fn(),
    setQueryData: vi.fn()
}));

vi.mock('$features/auth/index.svelte', () => ({
    accessToken: { current: 'test-token' }
}));

vi.mock('@exceptionless/fetchclient', () => ({
    useFetchClient: () => mocks.client
}));

vi.mock('@tanstack/svelte-query', () => ({
    createMutation: (factory: () => unknown) => factory(),
    createQuery: (factory: () => unknown) => factory(),
    useQueryClient: () => ({
        cancelQueries: mocks.cancelQueries,
        getQueriesData: mocks.getQueriesData,
        invalidateQueries: mocks.invalidateQueries,
        setQueriesData: mocks.setQueriesData,
        setQueryData: mocks.setQueryData
    })
}));

import {
    deleteRateNotificationRule,
    getRateNotificationRulesQuery,
    postRateNotificationRule,
    postSnoozeRateNotificationRule,
    postUnsnoozeRateNotificationRule,
    putRateNotificationRule
} from './api.svelte';

interface Mutation<TVariables, TData = unknown> {
    mutationFn: (variables: TVariables) => Promise<unknown>;
    onError?: (error: unknown, variables: TVariables, context: unknown) => void;
    onMutate?: (variables: TVariables) => Promise<unknown>;
    onSettled?: (data: TData | undefined, error: unknown, variables: TVariables) => void;
    onSuccess: (data: TData, variables: TVariables) => void;
}

describe('rate notification API', () => {
    beforeEach(() => {
        vi.clearAllMocks();
        mocks.client.delete.mockResolvedValue({});
        mocks.client.getJSON.mockResolvedValue({ data: [] });
        mocks.client.postJSON.mockResolvedValue({ data: { id: 'rule-id' } });
        mocks.client.putJSON.mockResolvedValue({ data: { id: 'rule-id' } });
        mocks.cancelQueries.mockResolvedValue(undefined);
        mocks.getQueriesData.mockReturnValue([]);
    });

    afterEach(() => {
        vi.useRealTimers();
    });

    it('reacts when route identifiers become available after query creation', async () => {
        // Arrange
        const routeState: { projectId?: string; userId?: string } = {};
        const query = getRateNotificationRulesQuery({
            route: {
                get projectId() {
                    return routeState.projectId;
                },
                get userId() {
                    return routeState.userId;
                }
            }
        }) as unknown as { enabled: () => boolean; queryFn: (context: { signal: AbortSignal }) => Promise<unknown> };
        expect(query.enabled()).toBe(false);

        // Act
        routeState.projectId = 'project-id';
        routeState.userId = 'user-id';
        await query.queryFn({ signal: new AbortController().signal });

        // Assert
        expect(query.enabled()).toBe(true);
        expect(mocks.client.getJSON).toHaveBeenCalledWith('users/user-id/projects/project-id/rate-notifications', {
            params: { limit: 50 },
            signal: expect.any(AbortSignal)
        });
    });

    it('uses mutation variables for every rule-specific route', async () => {
        // Arrange
        const route = { projectId: 'project-id', userId: 'user-id' };
        const body = {
            cooldown: '00:30:00',
            is_enabled: true,
            name: 'Errors',
            signal: RateNotificationSignal.Errors,
            stack_id: null,
            subject: RateNotificationSubject.Project,
            threshold: 10,
            window: '00:05:00'
        };
        const createMutation = postRateNotificationRule() as unknown as Mutation<{ body: typeof body; projectId: string; userId: string }>;
        const deleteMutation = deleteRateNotificationRule() as unknown as Mutation<{ projectId: string; ruleId: string; userId: string }>;
        const snoozeMutation = postSnoozeRateNotificationRule() as unknown as Mutation<{
            body: { duration_seconds: number };
            projectId: string;
            ruleId: string;
            userId: string;
        }>;
        const unsnoozeMutation = postUnsnoozeRateNotificationRule() as unknown as Mutation<{ projectId: string; ruleId: string; userId: string }>;
        const updateMutation = putRateNotificationRule() as unknown as Mutation<{
            body: { is_enabled: boolean };
            projectId: string;
            ruleId: string;
            userId: string;
        }>;

        // Act
        await createMutation.mutationFn({ ...route, body });
        await updateMutation.mutationFn({ ...route, body: { is_enabled: false }, ruleId: 'update-rule' });
        await snoozeMutation.mutationFn({ ...route, body: { duration_seconds: 3600 }, ruleId: 'snooze-rule' });
        await unsnoozeMutation.mutationFn({ ...route, ruleId: 'unsnooze-rule' });
        await deleteMutation.mutationFn({ ...route, ruleId: 'delete-rule' });

        // Assert
        expect(mocks.client.postJSON).toHaveBeenCalledWith('users/user-id/projects/project-id/rate-notifications', body);
        expect(mocks.client.putJSON).toHaveBeenCalledWith('users/user-id/projects/project-id/rate-notifications/update-rule', {
            is_enabled: false
        });
        expect(mocks.client.postJSON).toHaveBeenCalledWith(
            'users/user-id/projects/project-id/rate-notifications/snooze-rule/snooze',
            { duration_seconds: 3600 },
            { expectedStatusCodes: [200] }
        );
        expect(mocks.client.postJSON).toHaveBeenCalledWith(
            'users/user-id/projects/project-id/rate-notifications/unsnooze-rule/unsnooze',
            {},
            { expectedStatusCodes: [200] }
        );
        expect(mocks.client.delete).toHaveBeenCalledWith('users/user-id/projects/project-id/rate-notifications/delete-rule', {
            expectedStatusCodes: [204]
        });
    });

    it('updates snoozed rules immediately and refreshes after Elasticsearch consistency delay', () => {
        // Arrange
        vi.useFakeTimers();
        const route = { projectId: 'project-id', userId: 'user-id' };
        const mutation = postSnoozeRateNotificationRule() as unknown as Mutation<
            { body: { duration_seconds: number }; projectId: string; ruleId: string; userId: string },
            ViewRateNotificationRule
        >;
        const rule: ViewRateNotificationRule = {
            cooldown: '00:30:00',
            created_utc: '2026-07-10T00:00:00Z',
            id: 'rule-id',
            is_enabled: true,
            is_snoozed: true,
            name: 'Errors',
            organization_id: 'organization-id',
            project_id: route.projectId,
            signal: RateNotificationSignal.Errors,
            subject: RateNotificationSubject.Project,
            threshold: 10,
            updated_utc: '2026-07-10T00:01:00Z',
            user_id: route.userId,
            version: 2,
            window: '00:05:00'
        };

        // Act
        mutation.onSuccess(rule, { ...route, body: { duration_seconds: 3600 }, ruleId: rule.id });

        // Assert
        expect(mocks.setQueriesData).toHaveBeenCalledOnce();
        const update = mocks.setQueriesData.mock.calls[0]?.[1] as (response: { data: ViewRateNotificationRule[] }) => {
            data: ViewRateNotificationRule[];
        };
        const updated = update({ data: [{ ...rule, is_snoozed: false, version: 1 }] });
        expect(updated.data).toEqual([rule]);
        expect(mocks.invalidateQueries).not.toHaveBeenCalled();

        vi.advanceTimersByTime(1500);
        expect(mocks.invalidateQueries).toHaveBeenCalledWith({ queryKey: ['RateNotificationRule', route.userId, route.projectId] });
    });

    it('rolls back an optimistic rule update when the request fails', async () => {
        // Arrange
        const route = { projectId: 'project-id', userId: 'user-id' };
        const variables = { ...route, body: { is_enabled: false }, ruleId: 'rule-id' };
        const queryKey = ['RateNotificationRule', route.userId, route.projectId, { params: undefined }] as const;
        const previousResponse = { data: [{ id: variables.ruleId, is_enabled: true }] } as FetchClientResponse<ViewRateNotificationRule[]>;
        mocks.getQueriesData.mockReturnValue([[queryKey, previousResponse]]);
        const mutation = putRateNotificationRule() as unknown as Mutation<typeof variables, ViewRateNotificationRule>;

        // Act
        const context = await mutation.onMutate?.(variables);
        mutation.onError?.(new Error('request failed'), variables, context);

        // Assert
        expect(mocks.cancelQueries).toHaveBeenCalledWith({ queryKey: ['RateNotificationRule', route.userId, route.projectId] });
        expect(mocks.setQueriesData).toHaveBeenCalledOnce();
        expect(mocks.setQueryData).toHaveBeenCalledWith(queryKey, previousResponse);
    });
});
