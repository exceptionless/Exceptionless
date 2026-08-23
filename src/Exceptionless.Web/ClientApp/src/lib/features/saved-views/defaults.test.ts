import { describe, expect, it } from 'vitest';

import type { SavedView, ViewSavedViewDefaults } from './models';

import { getSavedViewDefaultHref } from './defaults';

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
        const defaults: ViewSavedViewDefaults = {
            organization_default: savedView({ id: 'organization-default', slug: 'organization-home' }),
            user_default: savedView({ id: 'user-default', slug: 'my-home', view_type: 'events' })
        };

        expect(getSavedViewDefaultHref(defaults)).toBe('/next/event/my-home');
    });

    it('uses the organization default when there is no personal default', () => {
        const defaults: ViewSavedViewDefaults = {
            organization_default: savedView({ id: 'organization-default', slug: 'organization-home' })
        };

        expect(getSavedViewDefaultHref(defaults)).toBe('/next/stack/organization-home');
    });

    it('falls back to the first Stacks saved view when no configured default is available', () => {
        const stackSavedViews = [savedView({ id: 'first-stack-view', slug: 'all' }), savedView({ id: 'second-stack-view', slug: 'errors' })];

        expect(getSavedViewDefaultHref(undefined, stackSavedViews)).toBe('/next/stack/all');
        expect(getSavedViewDefaultHref({}, stackSavedViews)).toBe('/next/stack/all');
    });

    it('falls back to the built-in Stacks route when there are no Stacks saved views', () => {
        expect(getSavedViewDefaultHref(undefined, [])).toBe('/next/stack');
        expect(getSavedViewDefaultHref({}, undefined)).toBe('/next/stack');
    });

    it('builds stream saved view links using the saved view identifier', () => {
        const defaults: ViewSavedViewDefaults = {
            user_default: savedView({ id: 'stream-default', view_type: 'stream' })
        };

        expect(getSavedViewDefaultHref(defaults)).toBe('/next/stream?saved=stream-default');
    });
});
