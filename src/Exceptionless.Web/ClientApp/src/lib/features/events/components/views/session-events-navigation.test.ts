import type { SavedView } from '$features/saved-views/models';

import { describe, expect, it } from 'vitest';

import { getSessionEventsPath } from './session-events-navigation';

function createSavedView(overrides: Partial<SavedView> = {}): SavedView {
    return {
        created_by_user_id: 'user-id',
        created_utc: '2026-08-29T00:00:00Z',
        id: 'saved-view-id',
        name: 'All',
        organization_id: 'organization-id',
        slug: 'all',
        updated_utc: '2026-08-29T00:00:00Z',
        version: 1,
        view_type: 'events',
        ...overrides
    };
}

describe('getSessionEventsPath', () => {
    it('uses the Events All saved view when it exists', () => {
        expect(getSessionEventsPath([createSavedView()])).toBe('/next/event/all');
    });

    it('matches the resolved All slug even when the view name changes', () => {
        expect(getSessionEventsPath([createSavedView({ name: 'Everything', slug: 'all' })])).toBe('/next/event/all');
    });

    it('prefers the shared All view when a private view already owns the all slug', () => {
        const privateAll = createSavedView({ id: 'private-all', user_id: 'user-id' });
        const sharedAll = createSavedView({ id: 'shared-all', slug: 'all-2' });

        expect(getSessionEventsPath([privateAll, sharedAll])).toBe('/next/event/all-2');
    });

    it('does not use a private All view when no shared All view exists', () => {
        expect(getSessionEventsPath([createSavedView({ user_id: 'user-id' })])).toBe('/next/event');
    });

    it('falls back to the generic Events route when All is unavailable', () => {
        expect(getSessionEventsPath([createSavedView({ name: 'Errors', slug: 'errors' })])).toBe('/next/event');
    });

    it('does not use an All view belonging to another resource', () => {
        expect(getSessionEventsPath([createSavedView({ view_type: 'stacks' })])).toBe('/next/event');
    });
});
