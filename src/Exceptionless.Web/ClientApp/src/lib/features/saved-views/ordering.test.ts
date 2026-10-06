import type { UserSavedViewOrderPreference } from '$generated/api';

import { describe, expect, it } from 'vitest';

import type { SavedView } from './models';

import { getPersonalSavedViewOrder, resolvePersonalSavedViewOrder } from './ordering';

function savedView(id: string, name: string, userId?: string): SavedView {
    return {
        id,
        name,
        organization_id: 'organization-id',
        slug: name.toLowerCase().replaceAll(' ', '-'),
        user_id: userId,
        view_type: 'events'
    } as SavedView;
}

describe('getPersonalSavedViewOrder', () => {
    it('combines duplicate order preferences without repeating identifiers', () => {
        const preferences = [
            {
                organization_id: 'organization-id',
                saved_view_ids: ['private-view', 'shared-view'],
                view_type: 'events'
            },
            {
                organization_id: 'organization-id',
                saved_view_ids: ['shared-view', 'new-view'],
                view_type: 'events'
            },
            {
                organization_id: 'other-organization',
                saved_view_ids: ['other-view'],
                view_type: 'events'
            },
            {
                organization_id: 'organization-id',
                saved_view_ids: ['other-section-view'],
                view_type: 'stacks'
            }
        ] as UserSavedViewOrderPreference[];

        expect(getPersonalSavedViewOrder(preferences, 'organization-id', 'events')).toEqual(['private-view', 'shared-view', 'new-view']);
    });
});

describe('resolvePersonalSavedViewOrder', () => {
    it('interleaves shared and private views and appends new views alphabetically', () => {
        const sharedView = savedView('shared-view', 'Zulu Shared');
        const privateView = savedView('private-view', 'Alpha Private', 'user-id');
        const newSharedView = savedView('new-shared-view', 'Beta Shared');
        const newPrivateView = savedView('new-private-view', 'Charlie Private', 'user-id');

        expect(
            resolvePersonalSavedViewOrder([sharedView, privateView, newPrivateView, newSharedView], ['private-view', 'missing-view', 'shared-view'])
        ).toEqual([privateView, sharedView, newSharedView, newPrivateView]);
    });

    it('uses alphabetical order when no personal order exists', () => {
        const zulu = savedView('zulu-view', 'Zulu');
        const alpha = savedView('alpha-view', 'Alpha');

        expect(resolvePersonalSavedViewOrder([zulu, alpha], [])).toEqual([alpha, zulu]);
    });
});
