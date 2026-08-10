import { ChangeType } from '$features/websockets/models';
import { QueryClient } from '@tanstack/svelte-query';
import { describe, expect, it, vi } from 'vitest';

import { invalidateStackQueries, queryKeys } from './api.svelte';

describe('invalidateStackQueries', () => {
    it('marks cached project stack lists stale without refetching them immediately', async () => {
        // Arrange
        const queryClient = new QueryClient();
        const projectListKey = queryKeys.project('project-id', { filter: 'status:open' });
        const otherProjectListKey = queryKeys.project('other-project-id', { filter: 'status:open' });
        queryClient.setQueryData(projectListKey, { data: [] });
        queryClient.setQueryData(otherProjectListKey, { data: [] });
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
        const listInvalidation = invalidateSpy.mock.calls.find(([filters]) => filters?.refetchType === 'none')?.[0];
        expect(listInvalidation).toMatchObject({
            queryKey: queryKeys.projects('project-id'),
            refetchType: 'none'
        });
        expect(listInvalidation?.predicate?.({ queryKey: projectListKey } as never)).toBe(true);
        expect(queryClient.getQueryState(projectListKey)?.isInvalidated).toBe(true);
        expect(queryClient.getQueryState(otherProjectListKey)?.isInvalidated).toBe(false);
    });
});
