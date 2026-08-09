import { ChangeType } from '$features/websockets/models';
import { QueryClient } from '@tanstack/svelte-query';
import { describe, expect, it, vi } from 'vitest';

import { invalidateStackQueries, queryKeys } from './api.svelte';

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
