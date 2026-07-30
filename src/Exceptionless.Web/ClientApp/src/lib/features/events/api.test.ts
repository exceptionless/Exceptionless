import type { CountResult } from '$shared/models';

import { ChangeType } from '$features/websockets/models';
import { QueryClient } from '@tanstack/svelte-query';
import { afterEach, describe, expect, it, vi } from 'vitest';

const fetchClientMocks = vi.hoisted(() => ({
    getJSON: vi.fn()
}));

const queryMocks = vi.hoisted(() => ({
    queryFn: undefined as ((context: { signal: AbortSignal }) => Promise<CountResult>) | undefined
}));

vi.mock('$features/auth/index.svelte', () => ({
    accessToken: { current: 'test-token' }
}));

vi.mock('@exceptionless/fetchclient', () => ({
    useFetchClient: () => ({ getJSON: fetchClientMocks.getJSON })
}));

vi.mock('@tanstack/svelte-query', async (importOriginal) => {
    const actual = await importOriginal<typeof import('@tanstack/svelte-query')>();
    return {
        ...actual,
        createQuery: vi.fn((factory: () => { queryFn: (context: { signal: AbortSignal }) => Promise<CountResult> }) => {
            const options = factory();
            queryMocks.queryFn = options.queryFn;
            return options;
        }),
        useQueryClient: vi.fn(() => new actual.QueryClient())
    };
});

import { queryKeys as stackQueryKeys } from '../stacks/api.svelte';
import {
    getOrganizationCountQuery,
    invalidatePersistentEventQueries,
    PERSISTENT_EVENT_DELETE_RECONCILE_DELAY,
    PERSISTENT_EVENT_DELETE_RECONCILE_EVENT,
    PERSISTENT_EVENT_DELETE_RECONCILE_RETRY_DELAY,
    queryKeys,
    schedulePersistentEventDeleteReconciliation
} from './api.svelte';

describe('getOrganizationCountQuery', () => {
    it('forwards stack mode with a stack-only filter to the count request', async () => {
        // Arrange
        fetchClientMocks.getJSON.mockResolvedValue({ data: { aggregations: {}, total: 0 } });
        getOrganizationCountQuery({
            params: {
                filter: 'critical:false',
                mode: 'stack_frequent'
            },
            route: { organizationId: 'organization-id' }
        });

        // Act
        const queryFn = queryMocks.queryFn;
        if (!queryFn) {
            throw new Error('Expected createQuery to register a query function.');
        }

        await queryFn({ signal: new AbortController().signal });

        // Assert
        expect(fetchClientMocks.getJSON).toHaveBeenCalledWith('/organizations/organization-id/events/count', {
            params: expect.objectContaining({
                filter: 'critical:false',
                mode: 'stack_frequent'
            }),
            signal: expect.any(AbortSignal)
        });
    });
});

afterEach(() => {
    fetchClientMocks.getJSON.mockReset();
    queryMocks.queryFn = undefined;
    vi.useRealTimers();
});

describe('invalidatePersistentEventQueries', () => {
    it('does not invalidate nested count aggregation queries for event updates', async () => {
        // Arrange
        const queryClient = new QueryClient();
        const invalidateSpy = vi.spyOn(queryClient, 'invalidateQueries').mockImplementation(async () => {});

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
    });

    it('leaves organization event list refreshes to the document event handler for bulk updates', async () => {
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
        expect(broadInvalidation?.predicate?.({ queryKey: queryKeys.organizationsCount('organization-id') } as never)).toBe(true);
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
