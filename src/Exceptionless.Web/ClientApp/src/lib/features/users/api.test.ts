import { QueryClient } from '@tanstack/svelte-query';
import { describe, expect, it } from 'vitest';

import type { ViewCurrentUser } from './models';

import { invalidateUserQueries, queryKeys, setCurrentUserSavedViewDefault, setCurrentUserSavedViewOrder } from './api.svelte';

describe('setCurrentUserSavedViewDefault', () => {
    it('replaces duplicate organization preferences in the current-user cache', () => {
        const queryClient = new QueryClient();
        const currentUser = {
            id: 'user-id',
            organization_preferences: [
                { default_saved_view_id: 'old-view-id', organization_id: 'organization-id' },
                { default_saved_view_id: 'duplicate-view-id', organization_id: 'organization-id' },
                { default_saved_view_id: 'other-view-id', organization_id: 'other-organization-id' }
            ]
        } as unknown as ViewCurrentUser;
        queryClient.setQueryData(queryKeys.me(), currentUser);

        setCurrentUserSavedViewDefault(queryClient, 'organization-id', 'new-view-id');

        expect(queryClient.getQueryData<ViewCurrentUser>(queryKeys.me())?.organization_preferences).toEqual([
            { default_saved_view_id: 'other-view-id', organization_id: 'other-organization-id' },
            { default_saved_view_id: 'new-view-id', organization_id: 'organization-id' }
        ]);
    });

    it('clears the organization preference', () => {
        const queryClient = new QueryClient();
        const currentUser = {
            id: 'user-id',
            organization_preferences: [{ default_saved_view_id: 'old-view-id', organization_id: 'organization-id' }]
        } as unknown as ViewCurrentUser;
        queryClient.setQueryData(queryKeys.me(), currentUser);

        setCurrentUserSavedViewDefault(queryClient, 'organization-id', null);

        expect(queryClient.getQueryData<ViewCurrentUser>(queryKeys.me())?.organization_preferences).toEqual([]);
    });

    it('preserves separate saved view ordering when the default changes or clears', () => {
        const queryClient = new QueryClient();
        const currentUser = {
            id: 'user-id',
            organization_preferences: [
                {
                    default_saved_view_id: 'old-view-id',
                    organization_id: 'organization-id'
                }
            ],
            saved_view_orders: [
                {
                    organization_id: 'organization-id',
                    saved_view_ids: ['private-view-id', 'shared-view-id'],
                    view_type: 'events'
                }
            ]
        } as unknown as ViewCurrentUser;
        queryClient.setQueryData(queryKeys.me(), currentUser);

        setCurrentUserSavedViewDefault(queryClient, 'organization-id', null);

        const updatedUser = queryClient.getQueryData<ViewCurrentUser>(queryKeys.me());
        expect(updatedUser?.organization_preferences).toEqual([]);
        expect(updatedUser?.saved_view_orders).toEqual([
            {
                organization_id: 'organization-id',
                saved_view_ids: ['private-view-id', 'shared-view-id'],
                view_type: 'events'
            }
        ]);
    });
});

describe('setCurrentUserSavedViewOrder', () => {
    it('updates one section while preserving the personal default and other sections', () => {
        const queryClient = new QueryClient();
        const currentUser = {
            id: 'user-id',
            organization_preferences: [
                {
                    default_saved_view_id: 'home-view-id',
                    organization_id: 'organization-id'
                }
            ],
            saved_view_orders: [
                { organization_id: 'organization-id', saved_view_ids: ['stack-view-id'], view_type: 'stacks' },
                { organization_id: 'other-organization-id', saved_view_ids: ['other-view-id'], view_type: 'events' }
            ]
        } as unknown as ViewCurrentUser;
        queryClient.setQueryData(queryKeys.me(), currentUser);

        setCurrentUserSavedViewOrder(queryClient, 'organization-id', 'events', ['private-view-id', 'shared-view-id']);

        expect(queryClient.getQueryData<ViewCurrentUser>(queryKeys.me())?.organization_preferences).toEqual([
            {
                default_saved_view_id: 'home-view-id',
                organization_id: 'organization-id'
            }
        ]);
        expect(queryClient.getQueryData<ViewCurrentUser>(queryKeys.me())?.saved_view_orders).toEqual([
            { organization_id: 'organization-id', saved_view_ids: ['stack-view-id'], view_type: 'stacks' },
            { organization_id: 'other-organization-id', saved_view_ids: ['other-view-id'], view_type: 'events' },
            {
                organization_id: 'organization-id',
                saved_view_ids: ['private-view-id', 'shared-view-id'],
                view_type: 'events'
            }
        ]);
    });
});

describe('invalidateUserQueries', () => {
    it('reports a change to the signed-in user so their Exie access is refreshed', async () => {
        // Arrange
        const queryClient = new QueryClient();
        queryClient.setQueryData(queryKeys.me(), { id: 'user-id', is_email_address_verified: true } as ViewCurrentUser);

        // Act
        const changed = await invalidateUserQueries(queryClient, { id: 'user-id' } as Parameters<typeof invalidateUserQueries>[1]);

        // Assert
        expect(changed).toBe(true);
        expect(queryClient.getQueryState(queryKeys.me())?.isInvalidated).toBe(true);
    });

    it('ignores changes to other users', async () => {
        // Arrange
        const queryClient = new QueryClient();
        queryClient.setQueryData(queryKeys.me(), { id: 'user-id' } as ViewCurrentUser);

        // Act
        const changed = await invalidateUserQueries(queryClient, { id: 'other-user-id' } as Parameters<typeof invalidateUserQueries>[1]);

        // Assert
        expect(changed).toBe(false);
        expect(queryClient.getQueryState(queryKeys.me())?.isInvalidated).toBe(false);
    });
});
