import { ChangeType } from '$features/websockets/models';
import { QueryClient } from '@tanstack/svelte-query';
import { afterEach, describe, expect, it, vi } from 'vitest';

import { createStackNotificationRefresher, invalidateStackQueries, queryKeys, STACK_NOTIFICATION_THROTTLE_MS } from './api.svelte';

afterEach(() => {
    vi.useRealTimers();
});

describe('createStackNotificationRefresher', () => {
    it('defers removal reconciliation without a leading refetch', async () => {
        // Arrange
        vi.useFakeTimers();
        const queryClient = new QueryClient();
        const invalidateSpy = vi.spyOn(queryClient, 'invalidateQueries').mockImplementation(async () => {});
        const refresher = createStackNotificationRefresher(queryClient);

        // Act
        refresher.schedule('organization-id', 'project-id', false);

        // Assert
        expect(invalidateSpy).not.toHaveBeenCalled();
        await vi.advanceTimersByTimeAsync(STACK_NOTIFICATION_THROTTLE_MS);
        expect(invalidateSpy).toHaveBeenCalledOnce();
    });

    it('performs a delayed reconciliation after an isolated notification', async () => {
        // Arrange
        vi.useFakeTimers();
        const queryClient = new QueryClient();
        const invalidateSpy = vi.spyOn(queryClient, 'invalidateQueries').mockImplementation(async () => {});
        const refresher = createStackNotificationRefresher(queryClient);

        // Act
        refresher.schedule('organization-id', 'project-id');

        // Assert
        expect(invalidateSpy).toHaveBeenCalledOnce();
        await vi.advanceTimersByTimeAsync(STACK_NOTIFICATION_THROTTLE_MS - 1);
        expect(invalidateSpy).toHaveBeenCalledOnce();

        await vi.advanceTimersByTimeAsync(1);
        expect(invalidateSpy).toHaveBeenCalledTimes(2);
        const reconciliation = invalidateSpy.mock.calls[1]?.[0];
        expect(reconciliation?.predicate?.({ queryKey: queryKeys.project('project-id') } as never)).toBe(true);
        expect(reconciliation?.predicate?.({ queryKey: queryKeys.organizationRollups('organization-id') } as never)).toBe(true);
        expect(reconciliation?.predicate?.({ queryKey: queryKeys.organizationRollupsStats('organization-id') } as never)).toBe(true);
    });

    it('refreshes matching active project lists immediately and at most once per throttle window', async () => {
        // Arrange
        vi.useFakeTimers();
        vi.setSystemTime(new Date('2026-08-09T20:00:00Z'));
        const queryClient = new QueryClient();
        const invalidateSpy = vi.spyOn(queryClient, 'invalidateQueries').mockImplementation(async () => {});
        const refresher = createStackNotificationRefresher(queryClient);

        // Act
        refresher.schedule('organization-id', 'project-id');
        for (let index = 0; index < 30; index++) {
            refresher.schedule(index % 2 === 0 ? 'organization-id' : 'other-organization-id', index % 2 === 0 ? 'project-id' : 'other-project-id');
        }

        // Assert
        expect(invalidateSpy).toHaveBeenCalledOnce();
        const firstInvalidation = invalidateSpy.mock.calls[0]?.[0];
        expect(firstInvalidation?.refetchType).toBe('active');
        expect(firstInvalidation?.predicate?.({ queryKey: queryKeys.project('project-id') } as never)).toBe(true);
        expect(firstInvalidation?.predicate?.({ queryKey: queryKeys.project('other-project-id') } as never)).toBe(false);
        expect(firstInvalidation?.predicate?.({ queryKey: queryKeys.organizationRollups('organization-id') } as never)).toBe(true);
        expect(firstInvalidation?.predicate?.({ queryKey: queryKeys.organizationRollups('other-organization-id') } as never)).toBe(false);
        expect(firstInvalidation?.predicate?.({ queryKey: queryKeys.id('stack-id') } as never)).toBe(false);

        await vi.advanceTimersByTimeAsync(STACK_NOTIFICATION_THROTTLE_MS - 1);
        expect(invalidateSpy).toHaveBeenCalledOnce();

        await vi.advanceTimersByTimeAsync(1);
        expect(invalidateSpy).toHaveBeenCalledTimes(2);
        const trailingInvalidation = invalidateSpy.mock.calls[1]?.[0];
        expect(trailingInvalidation?.predicate?.({ queryKey: queryKeys.project('project-id') } as never)).toBe(true);
        expect(trailingInvalidation?.predicate?.({ queryKey: queryKeys.project('other-project-id') } as never)).toBe(true);
        expect(trailingInvalidation?.predicate?.({ queryKey: queryKeys.organizationRollups('organization-id') } as never)).toBe(true);
        expect(trailingInvalidation?.predicate?.({ queryKey: queryKeys.organizationRollups('other-organization-id') } as never)).toBe(true);

        refresher.schedule('organization-id', 'project-id');
        expect(invalidateSpy).toHaveBeenCalledTimes(3);
        refresher.cancel();
        await vi.advanceTimersByTimeAsync(STACK_NOTIFICATION_THROTTLE_MS);
        expect(invalidateSpy).toHaveBeenCalledTimes(3);
    });
});

describe('invalidateStackQueries', () => {
    it('invalidates matching stack details without invalidating project lists', async () => {
        // Arrange
        const queryClient = new QueryClient();
        const projectListKey = queryKeys.project('project-id', { filter: 'status:open' });
        queryClient.setQueryData(projectListKey, { data: [] });
        const invalidateSpy = vi.spyOn(queryClient, 'invalidateQueries');

        // Act
        await invalidateStackQueries(queryClient, {
            change_type: ChangeType.Saved,
            data: {},
            id: 'stack-id',
            organization_id: 'organization-id',
            project_id: 'project-id',
            type: 'Stack'
        });

        // Assert
        expect(invalidateSpy).toHaveBeenCalledWith({ queryKey: queryKeys.id('stack-id') });
        expect(invalidateSpy).toHaveBeenCalledTimes(1);
        expect(queryClient.getQueryState(projectListKey)?.isInvalidated).toBe(false);
    });

    it('keeps project lists out of bulk notification invalidation', async () => {
        // Arrange
        const queryClient = new QueryClient();
        const invalidateSpy = vi.spyOn(queryClient, 'invalidateQueries').mockImplementation(async () => {});

        // Act
        await invalidateStackQueries(queryClient, {
            change_type: ChangeType.Saved,
            data: {},
            organization_id: 'organization-id',
            project_id: 'project-id',
            type: 'Stack'
        });

        // Assert
        const invalidation = invalidateSpy.mock.calls[0]?.[0];
        expect(invalidation?.predicate?.({ queryKey: queryKeys.project('project-id') } as never)).toBe(false);
        expect(invalidation?.predicate?.({ queryKey: queryKeys.id('stack-id') } as never)).toBe(true);
    });
});
