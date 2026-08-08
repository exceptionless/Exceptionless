import { QueryClient } from '@tanstack/svelte-query';
import { describe, expect, it, vi } from 'vitest';

import { invalidateOrganizationUsageQueries, invalidatePlanOverageQueries, queryKeys } from './api.svelte';

describe('invalidatePlanOverageQueries', () => {
    it('invalidates only the affected organization state', async () => {
        // Arrange
        const queryClient = new QueryClient();
        const invalidateSpy = vi.spyOn(queryClient, 'invalidateQueries').mockImplementation(async () => {});

        // Act
        await invalidatePlanOverageQueries(queryClient, {
            is_hourly: false,
            organization_id: 'organization-id'
        });

        // Assert
        expect(invalidateSpy).toHaveBeenCalledTimes(3);
        expect(invalidateSpy).toHaveBeenCalledWith({ exact: true, queryKey: queryKeys.id('organization-id', undefined) });
        expect(invalidateSpy).toHaveBeenCalledWith({ exact: true, queryKey: queryKeys.id('organization-id', 'stats') });
        expect(invalidateSpy).toHaveBeenCalledWith({ queryKey: queryKeys.list(undefined) });
    });
});

describe('invalidateOrganizationUsageQueries', () => {
    it('invalidates organization lists when there is no active organization', async () => {
        // Arrange
        const queryClient = new QueryClient();
        const invalidateSpy = vi.spyOn(queryClient, 'invalidateQueries').mockImplementation(async () => {});

        // Act
        await invalidateOrganizationUsageQueries(queryClient);

        // Assert
        expect(invalidateSpy).toHaveBeenCalledOnce();
        expect(invalidateSpy).toHaveBeenCalledWith({ queryKey: queryKeys.list(undefined) });
    });
});
