import type { UserOrganizationPreference } from '$generated/api';

import { describe, expect, it } from 'vitest';

import type { SavedView } from './models';

import { getSavedViewDefaultHref, resolveSavedViewDefaults } from './defaults';

function savedView(overrides: Partial<SavedView> = {}): SavedView {
    return {
        created_by_user_id: 'user-id',
        created_utc: '2026-08-23T00:00:00Z',
        id: 'saved-view-id',
        name: 'Home',
        organization_id: 'organization-id',
        slug: 'home',
        updated_utc: '2026-08-23T00:00:00Z',
        version: 1,
        view_type: 'stacks',
        ...overrides
    };
}

describe('getSavedViewDefaultHref', () => {
    it('uses the personal default before the organization default', () => {
        const savedViews = [
            savedView({ id: 'organization-default', slug: 'organization-home' }),
            savedView({ id: 'user-default', slug: 'my-home', view_type: 'events' })
        ];
        const defaults = resolveSavedViewDefaults({
            organizationDefaultSavedViewId: 'organization-default',
            organizationId: 'organization-id',
            organizationPreferences: [preference('user-default')],
            savedViews
        });

        expect(getSavedViewDefaultHref(defaults, savedViews)).toBe('/next/event/my-home');
    });

    it('uses the organization default when there is no personal default', () => {
        const savedViews = [savedView({ id: 'organization-default', slug: 'organization-home' })];
        const defaults = resolveSavedViewDefaults({
            organizationDefaultSavedViewId: 'organization-default',
            organizationId: 'organization-id',
            savedViews
        });

        expect(getSavedViewDefaultHref(defaults, savedViews)).toBe('/next/stack/organization-home');
    });

    it('skips missing duplicate personal defaults', () => {
        const savedViews = [savedView({ id: 'valid-default', slug: 'valid-home' })];
        const defaults = resolveSavedViewDefaults({
            organizationId: 'organization-id',
            organizationPreferences: [preference('missing-default'), preference('valid-default'), preference('valid-default')],
            savedViews
        });

        expect(defaults.userDefault?.id).toBe('valid-default');
        expect(getSavedViewDefaultHref(defaults, savedViews)).toBe('/next/stack/valid-home');
    });

    it('ignores a private organization default', () => {
        const savedViews = [
            savedView({ id: 'private-default', slug: 'private-home', user_id: 'user-id', view_type: 'events' }),
            savedView({ id: 'first-stack-view', slug: 'all' })
        ];
        const defaults = resolveSavedViewDefaults({
            organizationDefaultSavedViewId: 'private-default',
            organizationId: 'organization-id',
            savedViews
        });

        expect(defaults.organizationDefault).toBeUndefined();
        expect(getSavedViewDefaultHref(defaults, savedViews)).toBe('/next/stack/all');
    });

    it('falls back to the first Stacks saved view when no configured default is available', () => {
        const savedViews = [
            savedView({ id: 'event-view', slug: 'recent', view_type: 'events' }),
            savedView({ id: 'first-stack-view', slug: 'all' }),
            savedView({ id: 'second-stack-view', slug: 'errors' })
        ];

        expect(getSavedViewDefaultHref({}, savedViews)).toBe('/next/stack/all');
    });

    it('falls back to the built-in Stacks route when there are no Stacks saved views', () => {
        expect(getSavedViewDefaultHref({}, [])).toBe('/next/stack');
        expect(getSavedViewDefaultHref({}, undefined)).toBe('/next/stack');
    });

    it('builds stream saved view links using the saved view identifier', () => {
        const savedViews = [savedView({ id: 'stream-default', view_type: 'stream' })];
        const defaults = resolveSavedViewDefaults({
            organizationId: 'organization-id',
            organizationPreferences: [preference('stream-default')],
            savedViews
        });

        expect(getSavedViewDefaultHref(defaults, savedViews)).toBe('/next/stream?saved=stream-default');
    });

    it('builds Sessions saved view links for a configured home view', () => {
        const savedViews = [savedView({ id: 'sessions-default', slug: 'active', view_type: 'sessions' })];
        const defaults = resolveSavedViewDefaults({
            organizationId: 'organization-id',
            organizationPreferences: [preference('sessions-default')],
            savedViews
        });

        expect(getSavedViewDefaultHref(defaults, savedViews)).toBe('/next/sessions/active');
    });
});

function preference(defaultSavedViewId: string): UserOrganizationPreference {
    return {
        default_saved_view_id: defaultSavedViewId,
        organization_id: 'organization-id',
        saved_view_order: {}
    };
}
