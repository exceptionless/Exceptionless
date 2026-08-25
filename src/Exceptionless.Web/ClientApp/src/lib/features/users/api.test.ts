import { QueryClient } from '@tanstack/svelte-query';
import { describe, expect, it } from 'vitest';

import type { ViewCurrentUser } from './models';

import { queryKeys, setCurrentUserSavedViewDefault } from './api.svelte';

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
        } as ViewCurrentUser;
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
        } as ViewCurrentUser;
        queryClient.setQueryData(queryKeys.me(), currentUser);

        setCurrentUserSavedViewDefault(queryClient, 'organization-id', null);

        expect(queryClient.getQueryData<ViewCurrentUser>(queryKeys.me())?.organization_preferences).toEqual([]);
    });
});
