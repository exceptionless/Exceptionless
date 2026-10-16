import { QueryClient } from '@tanstack/svelte-query';
import { describe, expect, it, vi } from 'vitest';

import { queryKeys as eventQueryKeys } from '#features/events/api.svelte.ts';
import { queryKeys as stackQueryKeys } from '#features/stacks/api.svelte.ts';
import { ChangeType } from '#features/websockets/models.ts';

import { invalidateProjectQueries, queryKeys } from './api.svelte';

describe('invalidateProjectQueries', () => {
    it('invalidates project caches and summaries that embed the project name', async () => {
        // Arrange
        const queryClient = new QueryClient();
        const invalidateSpy = vi.spyOn(queryClient, 'invalidateQueries').mockImplementation(async () => {});

        // Act
        await invalidateProjectQueries(queryClient, {
            change_type: ChangeType.Saved,
            data: {},
            id: 'project-id',
            organization_id: 'organization-id',
            type: 'Project'
        });

        // Assert
        expect(invalidateSpy).toHaveBeenCalledWith({ queryKey: queryKeys.id('project-id') });
        expect(invalidateSpy).toHaveBeenCalledWith({ queryKey: queryKeys.organization('organization-id') });
        expect(invalidateSpy).toHaveBeenCalledWith({ queryKey: queryKeys.projects() });
        expect(invalidateSpy).toHaveBeenCalledWith({ queryKey: eventQueryKeys.type });
        expect(invalidateSpy).toHaveBeenCalledWith({ queryKey: stackQueryKeys.type });
    });
});
