import { QueryClient } from '@tanstack/svelte-query';
import { describe, expect, it, vi } from 'vitest';

import type { ViewOrganization } from './models';

import { invalidateOrganizationUsageQueries, invalidatePlanOverageQueries, queryKeys, setOrganizationDefaultSavedView } from './api.svelte';

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

describe('setOrganizationDefaultSavedView', () => {
    it('updates individual and list caches', () => {
        const queryClient = new QueryClient();
        const organization = { id: 'organization-id', name: 'Test' } as ViewOrganization;
        queryClient.setQueryData(queryKeys.id(organization.id, undefined), organization);
        queryClient.setQueryData([...queryKeys.list(undefined), { params: {} }], { data: [organization] });

        setOrganizationDefaultSavedView(queryClient, organization.id, 'saved-view-id');

        expect(queryClient.getQueryData<ViewOrganization>(queryKeys.id(organization.id, undefined))?.default_saved_view_id).toBe('saved-view-id');
        expect(queryClient.getQueryData<{ data: ViewOrganization[] }>([...queryKeys.list(undefined), { params: {} }])?.data[0]?.default_saved_view_id).toBe(
            'saved-view-id'
        );
    });
});
