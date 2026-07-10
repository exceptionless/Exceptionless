import type { ViewRateNotificationRule } from '$generated/api';

import { RateNotificationSignal, RateNotificationSubject } from '$generated/api';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';

const mocks = vi.hoisted(() => ({
    client: {
        delete: vi.fn(),
        getJSON: vi.fn(),
        postJSON: vi.fn(),
        putJSON: vi.fn()
    },
    invalidateQueries: vi.fn(),
    setQueriesData: vi.fn()
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
    useQueryClient: () => ({ invalidateQueries: mocks.invalidateQueries, setQueriesData: mocks.setQueriesData })
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
    onSuccess: (data: TData, variables: TVariables) => void;
}

describe('rate notification API', () => {
    beforeEach(() => {
        vi.clearAllMocks();
        mocks.client.delete.mockResolvedValue({});
        mocks.client.getJSON.mockResolvedValue({ data: [] });
        mocks.client.postJSON.mockResolvedValue({ data: { id: 'rule-id' } });
        mocks.client.putJSON.mockResolvedValue({ data: { id: 'rule-id' } });
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
        const createMutation = postRateNotificationRule({ route }) as unknown as Mutation<typeof body>;
        const deleteMutation = deleteRateNotificationRule({ route }) as unknown as Mutation<string>;
        const snoozeMutation = postSnoozeRateNotificationRule({ route }) as unknown as Mutation<{
            body: { duration_seconds: number };
            ruleId: string;
        }>;
        const unsnoozeMutation = postUnsnoozeRateNotificationRule({ route }) as unknown as Mutation<string>;
        const updateMutation = putRateNotificationRule({ route }) as unknown as Mutation<{ body: { is_enabled: boolean }; ruleId: string }>;

        // Act
        await createMutation.mutationFn(body);
        await updateMutation.mutationFn({ body: { is_enabled: false }, ruleId: 'update-rule' });
        await snoozeMutation.mutationFn({ body: { duration_seconds: 3600 }, ruleId: 'snooze-rule' });
        await unsnoozeMutation.mutationFn('unsnooze-rule');
        await deleteMutation.mutationFn('delete-rule');

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
        const mutation = postSnoozeRateNotificationRule({ route }) as unknown as Mutation<
            { body: { duration_seconds: number }; ruleId: string },
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
        mutation.onSuccess(rule, { body: { duration_seconds: 3600 }, ruleId: rule.id });

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
});
