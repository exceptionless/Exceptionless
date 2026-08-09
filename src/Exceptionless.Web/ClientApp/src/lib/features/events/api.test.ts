import { ChangeType } from '$features/websockets/models';
import { QueryClient } from '@tanstack/svelte-query';
import { afterEach, describe, expect, it, vi } from 'vitest';

import { queryKeys as stackQueryKeys } from '../stacks/api.svelte';
import {
    invalidatePersistentEventQueries,
    PERSISTENT_EVENT_DELETE_RECONCILE_DELAY,
    PERSISTENT_EVENT_DELETE_RECONCILE_EVENT,
    PERSISTENT_EVENT_DELETE_RECONCILE_RETRY_DELAY,
    queryKeys,
    schedulePersistentEventDeleteReconciliation
} from './api.svelte';

afterEach(() => {
    vi.useRealTimers();
});

describe('invalidatePersistentEventQueries', () => {
    it('invalidates matching event details without invalidating organization dashboards', async () => {
        // Arrange
        const queryClient = new QueryClient();
        const eventListKey = queryKeys.organizationsEvents('organization-id', { mode: 'summary' });
        const eventCountKey = queryKeys.organizationsCount('organization-id');
        queryClient.setQueryData(eventListKey, { data: [] });
        queryClient.setQueryData(eventCountKey, { total: 0 });
        const invalidateSpy = vi.spyOn(queryClient, 'invalidateQueries');

        // Act
        await invalidatePersistentEventQueries(queryClient, {
            change_type: ChangeType.Saved,
            data: {},
            id: 'event-id',
            organization_id: 'organization-id',
            project_id: 'project-id',
            stack_id: 'stack-id',
            type: 'PersistentEvent'
        });

        // Assert
        expect(invalidateSpy).toHaveBeenCalledWith({ queryKey: queryKeys.id('event-id') });
        expect(invalidateSpy).toHaveBeenCalledWith({ exact: true, queryKey: queryKeys.stacks('stack-id') });
        expect(invalidateSpy).toHaveBeenCalledWith({ exact: true, queryKey: queryKeys.projects('project-id') });
        expect(invalidateSpy).toHaveBeenCalledWith({ exact: true, queryKey: queryKeys.organizations('organization-id') });
        expect(invalidateSpy).not.toHaveBeenCalledWith({ queryKey: queryKeys.stacks('stack-id') });
        expect(queryClient.getQueryState(eventListKey)?.isInvalidated).toBe(false);
        expect(queryClient.getQueryState(eventCountKey)?.isInvalidated).toBe(false);
    });

    it('keeps organization dashboard queries out of bulk notification invalidation', async () => {
        // Arrange
        const queryClient = new QueryClient();
        const invalidateSpy = vi.spyOn(queryClient, 'invalidateQueries').mockImplementation(async () => {});

        // Act
        await invalidatePersistentEventQueries(queryClient, {
            change_type: ChangeType.Saved,
            data: {},
            organization_id: 'organization-id',
            project_id: 'project-id',
            type: 'PersistentEvent'
        });

        // Assert
        expect(invalidateSpy).toHaveBeenCalledWith({ exact: true, queryKey: queryKeys.projects('project-id') });
        expect(invalidateSpy).toHaveBeenCalledWith({ exact: true, queryKey: queryKeys.organizations('organization-id') });

        const broadInvalidation = invalidateSpy.mock.calls.find(([filters]) => filters?.queryKey === queryKeys.type)?.[0];
        expect(broadInvalidation?.predicate?.({ queryKey: queryKeys.organizationsEvents('organization-id') } as never)).toBe(false);
        expect(broadInvalidation?.predicate?.({ queryKey: queryKeys.organizationsCount('organization-id') } as never)).toBe(false);
        expect(broadInvalidation?.predicate?.({ queryKey: queryKeys.id('event-id') } as never)).toBe(true);
    });
});

describe('schedulePersistentEventDeleteReconciliation', () => {
    it('notifies manual grids immediately and invalidates query grids after the consistency delay', async () => {
        vi.useFakeTimers();
        const queryClient = new QueryClient();
        const invalidateSpy = vi.spyOn(queryClient, 'invalidateQueries').mockImplementation(async () => {});
        const reconcileListener = vi.fn();
        const eventTarget = new EventTarget();
        eventTarget.addEventListener(PERSISTENT_EVENT_DELETE_RECONCILE_EVENT, reconcileListener);

        schedulePersistentEventDeleteReconciliation(queryClient, eventTarget);

        expect(reconcileListener).toHaveBeenCalledOnce();
        expect(invalidateSpy).toHaveBeenCalledWith({ queryKey: stackQueryKeys.type });

        await vi.advanceTimersByTimeAsync(PERSISTENT_EVENT_DELETE_RECONCILE_DELAY);

        expect(invalidateSpy).toHaveBeenCalledWith({
            predicate: expect.any(Function),
            queryKey: queryKeys.type
        });
        expect(invalidateSpy).toHaveBeenCalledWith({ queryKey: stackQueryKeys.type });
        expect(reconcileListener).toHaveBeenCalledOnce();

        await vi.advanceTimersByTimeAsync(PERSISTENT_EVENT_DELETE_RECONCILE_RETRY_DELAY - PERSISTENT_EVENT_DELETE_RECONCILE_DELAY);

        expect(reconcileListener).toHaveBeenCalledTimes(2);
        expect(invalidateSpy).toHaveBeenCalledTimes(5);

        const persistentEventInvalidations = invalidateSpy.mock.calls.flatMap(([filters]) => (filters?.queryKey === queryKeys.type ? [filters] : []));
        expect(persistentEventInvalidations).toHaveLength(2);
        persistentEventInvalidations.forEach((filters) => {
            expect(filters.predicate?.({ queryKey: queryKeys.organizationsEvents('organization-id') } as never)).toBe(false);
            expect(filters.predicate?.({ queryKey: queryKeys.organizationsCount('organization-id') } as never)).toBe(true);
        });
    });
});
