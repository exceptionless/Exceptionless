import { ChangeType } from '$features/websockets/models';
import { QueryClient } from '@tanstack/svelte-query';
import { afterEach, describe, expect, it, vi } from 'vitest';

import type { PersistentEvent } from './models';

import { queryKeys as stackQueryKeys } from '../stacks/api.svelte';
import {
    createEventWithNavigationQueryOptions,
    createOrganizationEventNotificationRefresher,
    type GetEventRequest,
    invalidatePersistentEventQueries,
    ORGANIZATION_EVENT_NOTIFICATION_THROTTLE_MS,
    PERSISTENT_EVENT_DELETE_RECONCILE_DELAY,
    PERSISTENT_EVENT_DELETE_RECONCILE_EVENT,
    PERSISTENT_EVENT_DELETE_RECONCILE_RETRY_DELAY,
    queryKeys,
    schedulePersistentEventDeleteReconciliation
} from './api.svelte';

const fetchClientMocks = vi.hoisted(() => ({ getJSON: vi.fn() }));

vi.mock('@foundatiofx/fetchclient', () => ({
    useFetchClient: () => ({ getJSON: fetchClientMocks.getJSON })
}));

afterEach(() => {
    vi.useRealTimers();
});

describe('createOrganizationEventNotificationRefresher', () => {
    it('defers removal reconciliation without a leading refetch', async () => {
        // Arrange
        vi.useFakeTimers();
        const queryClient = new QueryClient();
        const invalidateSpy = vi.spyOn(queryClient, 'invalidateQueries').mockImplementation(async () => {});
        const refresher = createOrganizationEventNotificationRefresher(queryClient);

        // Act
        refresher.schedule('organization-id', false);

        // Assert
        expect(invalidateSpy).not.toHaveBeenCalled();
        await vi.advanceTimersByTimeAsync(ORGANIZATION_EVENT_NOTIFICATION_THROTTLE_MS);
        expect(invalidateSpy).toHaveBeenCalledOnce();
    });

    it('performs a delayed reconciliation after an isolated notification', async () => {
        // Arrange
        vi.useFakeTimers();
        const queryClient = new QueryClient();
        const invalidateSpy = vi.spyOn(queryClient, 'invalidateQueries').mockImplementation(async () => {});
        const refresher = createOrganizationEventNotificationRefresher(queryClient);

        // Act
        refresher.schedule('organization-id');

        // Assert
        expect(invalidateSpy).toHaveBeenCalledOnce();
        await vi.advanceTimersByTimeAsync(ORGANIZATION_EVENT_NOTIFICATION_THROTTLE_MS - 1);
        expect(invalidateSpy).toHaveBeenCalledOnce();

        await vi.advanceTimersByTimeAsync(1);
        expect(invalidateSpy).toHaveBeenCalledTimes(2);
        const reconciliation = invalidateSpy.mock.calls[1]?.[0];
        expect(reconciliation?.predicate?.({ queryKey: queryKeys.organizationsEvents('organization-id') } as never)).toBe(true);
        expect(reconciliation?.predicate?.({ queryKey: queryKeys.organizationsCount('organization-id') } as never)).toBe(true);
    });

    it('refreshes matching active dashboards immediately and at most once per throttle window', async () => {
        // Arrange
        vi.useFakeTimers();
        vi.setSystemTime(new Date('2026-08-09T20:00:00Z'));
        const queryClient = new QueryClient();
        const invalidateSpy = vi.spyOn(queryClient, 'invalidateQueries').mockImplementation(async () => {});
        const refresher = createOrganizationEventNotificationRefresher(queryClient);

        // Act
        refresher.schedule('organization-id');
        for (let index = 0; index < 30; index++) {
            refresher.schedule(index % 2 === 0 ? 'organization-id' : 'other-organization-id');
        }

        // Assert
        expect(invalidateSpy).toHaveBeenCalledOnce();
        const firstInvalidation = invalidateSpy.mock.calls[0]?.[0];
        expect(firstInvalidation?.refetchType).toBe('active');
        expect(firstInvalidation?.predicate?.({ queryKey: queryKeys.organizationsEvents('organization-id') } as never)).toBe(true);
        expect(firstInvalidation?.predicate?.({ queryKey: queryKeys.organizationsCount('organization-id') } as never)).toBe(true);
        expect(firstInvalidation?.predicate?.({ queryKey: queryKeys.organizationsEvents('other-organization-id') } as never)).toBe(false);
        expect(firstInvalidation?.predicate?.({ queryKey: queryKeys.id('event-id') } as never)).toBe(false);

        await vi.advanceTimersByTimeAsync(ORGANIZATION_EVENT_NOTIFICATION_THROTTLE_MS - 1);
        expect(invalidateSpy).toHaveBeenCalledOnce();

        await vi.advanceTimersByTimeAsync(1);
        expect(invalidateSpy).toHaveBeenCalledTimes(2);
        const trailingInvalidation = invalidateSpy.mock.calls[1]?.[0];
        expect(trailingInvalidation?.predicate?.({ queryKey: queryKeys.organizationsEvents('organization-id') } as never)).toBe(true);
        expect(trailingInvalidation?.predicate?.({ queryKey: queryKeys.organizationsCount('other-organization-id') } as never)).toBe(true);

        refresher.schedule('organization-id');
        expect(invalidateSpy).toHaveBeenCalledTimes(3);
        refresher.cancel();
        await vi.advanceTimersByTimeAsync(ORGANIZATION_EVENT_NOTIFICATION_THROTTLE_MS);
        expect(invalidateSpy).toHaveBeenCalledTimes(3);
    });
});

describe('createEventWithNavigationQueryOptions', () => {
    it('caches a delayed response under the event ID that started the request', async () => {
        // Arrange
        const queryClient = new QueryClient();
        const request: GetEventRequest = {
            params: { time: '1h' },
            route: { id: 'event-a' }
        };
        const event = { id: 'event-a' } as PersistentEvent;
        let resolveResponse: ((response: { data: PersistentEvent; meta: { links: Record<string, never> } }) => void) | undefined;
        fetchClientMocks.getJSON.mockImplementationOnce(
            () =>
                new Promise((resolve) => {
                    resolveResponse = resolve;
                })
        );
        const options = createEventWithNavigationQueryOptions(request, queryClient);

        // Act
        const resultPromise = options.queryFn();
        request.route.id = 'event-b';
        request.params = { time: '7d' };
        resolveResponse?.({ data: event, meta: { links: {} } });
        const result = await resultPromise;

        // Assert
        expect(fetchClientMocks.getJSON).toHaveBeenCalledWith('events/event-a', {
            params: expect.objectContaining({ time: '1h' })
        });
        expect(options.queryKey).toEqual([...queryKeys.id('event-a'), 'withNavigation', { time: '1h' }]);
        expect(result.event).toBe(event);
        expect(queryClient.getQueryData(queryKeys.id('event-a'))).toBe(event);
        expect(queryClient.getQueryData(queryKeys.id('event-b'))).toBeUndefined();
    });
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
