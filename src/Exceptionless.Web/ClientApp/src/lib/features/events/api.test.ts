import { ChangeType } from '$features/websockets/models';
import { QueryClient } from '@tanstack/svelte-query';
import { describe, expect, it, vi } from 'vitest';

const fetchClientMocks = vi.hoisted(() => ({
    getJSON: vi.fn()
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
        createQuery: vi.fn((factory) => factory()),
        useQueryClient: vi.fn(() => new actual.QueryClient())
    };
});

import { getOrganizationCountQuery, invalidatePersistentEventQueries, queryKeys } from './api.svelte';

describe('getOrganizationCountQuery', () => {
    it('forwards stack mode with a stack-only filter to the count request', async () => {
        // Arrange
        fetchClientMocks.getJSON.mockResolvedValue({ data: { aggregations: {}, total: 0 } });
        const query = getOrganizationCountQuery({
            params: {
                filter: 'critical:false',
                mode: 'stack_frequent'
            },
            route: { organizationId: 'organization-id' }
        }) as unknown as { queryFn: (context: { signal: AbortSignal }) => Promise<unknown> };

        // Act
        await query.queryFn({ signal: new AbortController().signal });

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
});
