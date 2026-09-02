import { QueryClient } from '@tanstack/svelte-query';
import { describe, expect, it } from 'vitest';

import type { ViewCurrentUser } from './models';

import { queryKeys, setCurrentUserSavedViewDefault, setCurrentUserSavedViewOrder } from './api.svelte';

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
            { default_saved_view_id: 'new-view-id', organization_id: 'organization-id', saved_view_order: {} }
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

    it('preserves saved view ordering when the default changes or clears', () => {
        const queryClient = new QueryClient();
        const currentUser = {
            id: 'user-id',
            organization_preferences: [
                {
                    default_saved_view_id: 'old-view-id',
                    organization_id: 'organization-id',
                    saved_view_order: { events: ['private-view-id', 'shared-view-id'] }
                }
            ]
        } as unknown as ViewCurrentUser;
        queryClient.setQueryData(queryKeys.me(), currentUser);

        setCurrentUserSavedViewDefault(queryClient, 'organization-id', null);

        expect(queryClient.getQueryData<ViewCurrentUser>(queryKeys.me())?.organization_preferences).toEqual([
            {
                default_saved_view_id: null,
                organization_id: 'organization-id',
                saved_view_order: { events: ['private-view-id', 'shared-view-id'] }
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
                    organization_id: 'organization-id',
                    saved_view_order: { stacks: ['stack-view-id'] }
                }
            ]
        } as unknown as ViewCurrentUser;
        queryClient.setQueryData(queryKeys.me(), currentUser);

        setCurrentUserSavedViewOrder(queryClient, 'organization-id', 'events', ['private-view-id', 'shared-view-id']);

        expect(queryClient.getQueryData<ViewCurrentUser>(queryKeys.me())?.organization_preferences).toEqual([
            {
                default_saved_view_id: 'home-view-id',
                organization_id: 'organization-id',
                saved_view_order: {
                    events: ['private-view-id', 'shared-view-id'],
                    stacks: ['stack-view-id']
                }
            }
        ]);
    });
});
