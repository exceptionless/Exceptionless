import { QueryClient } from '@tanstack/svelte-query';
import { describe, expect, it, vi } from 'vitest';

import { queryKeys as userQueryKeys } from '#features/users/api.svelte.ts';
import { ChangeType } from '#features/websockets/models.ts';

import type { ViewOrganization } from './models';

import {
    invalidateOrganizationUsageQueries,
    invalidatePlanOverageQueries,
    invalidateUserMembershipQueries,
    queryKeys,
    setOrganizationDefaultSavedView
} from './api.svelte';

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

describe('invalidateUserMembershipQueries', () => {
    const organizationsKey = [...queryKeys.list(undefined), { params: {} }];
    const organizationKey = queryKeys.id('organization-id', undefined);
    const membersKey = [...userQueryKeys.organization('organization-id'), { params: {} }];

    function seed() {
        const queryClient = new QueryClient();
        for (const key of [organizationsKey, organizationKey, membersKey, userQueryKeys.me()]) {
            queryClient.setQueryData(key, {});
        }

        return queryClient;
    }

    const isStale = (queryClient: QueryClient, key: readonly unknown[]) => queryClient.getQueryState(key)?.isInvalidated;

    it.each([ChangeType.Added, ChangeType.Removed])("refreshes the current user's organizations and profile after change %s", async (changeType) => {
        // Arrange
        const queryClient = seed();

        // Act
        await invalidateUserMembershipQueries(queryClient, { change_type: changeType, organization_id: 'organization-id', user_id: 'me' }, 'me');

        // Assert
        expect(isStale(queryClient, organizationsKey)).toBe(true);
        expect(isStale(queryClient, userQueryKeys.me())).toBe(true);
        expect(isStale(queryClient, membersKey)).toBe(true);
        expect(isStale(queryClient, organizationKey)).toBe(changeType === ChangeType.Removed);
    });

    it('refreshes only the member list when another user changes', async () => {
        // Arrange
        const queryClient = seed();

        // Act
        await invalidateUserMembershipQueries(
            queryClient,
            { change_type: ChangeType.Added, organization_id: 'organization-id', user_id: 'someone-else' },
            'me'
        );

        // Assert
        expect(isStale(queryClient, membersKey)).toBe(true);
        expect(isStale(queryClient, organizationsKey)).toBe(false);
        expect(isStale(queryClient, userQueryKeys.me())).toBe(false);
        expect(isStale(queryClient, organizationKey)).toBe(false);
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
