import type { SavedView } from '$features/saved-views/models';

import { describe, expect, it } from 'vitest';

import { getSessionEventsHref, getSessionEventsPath } from './session-events-navigation';

function createSavedView(overrides: Partial<SavedView> = {}): SavedView {
    return {
        created_by_user_id: 'user-id',
        created_utc: '2026-08-29T00:00:00Z',
        id: 'saved-view-id',
        name: 'All',
        organization_id: 'organization-id',
        predefined_key: 'events:all',
        slug: 'all',
        updated_utc: '2026-08-29T00:00:00Z',
        version: 1,
        view_type: 'events',
        ...overrides
    };
}

describe('getSessionEventsPath', () => {
    it('waits for saved views before enabling navigation', () => {
        expect(getSessionEventsPath(undefined, true)).toBeUndefined();
    });

    it('uses the Events All saved view when it exists', () => {
        expect(getSessionEventsPath([createSavedView()])).toBe('/next/event/all');
    });

    it('uses the immutable predefined key after the Events All view is renamed', () => {
        expect(getSessionEventsPath([createSavedView({ name: 'Everything', slug: 'everything' })])).toBe('/next/event/everything');
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
        expect(getSessionEventsPath([createSavedView({ name: 'Errors', predefined_key: 'events:errors', slug: 'errors' })])).toBe('/next/event');
    });

    it('does not mistake an unrelated all-N slug for the All view', () => {
        expect(getSessionEventsPath([createSavedView({ name: 'Everything', predefined_key: undefined, slug: 'all-2' })])).toBe('/next/event');
    });

    it('supports a legacy unkeyed shared All view', () => {
        expect(getSessionEventsPath([createSavedView({ predefined_key: undefined })])).toBe('/next/event/all');
    });

    it('does not use a differently keyed view that was renamed to All', () => {
        expect(getSessionEventsPath([createSavedView({ predefined_key: 'events:errors' })])).toBe('/next/event');
    });

    it('does not use an All view belonging to another resource', () => {
        expect(getSessionEventsPath([createSavedView({ view_type: 'stacks' })])).toBe('/next/event');
    });
});

describe('getSessionEventsHref', () => {
    it('keeps the saved view path while replacing every saved filter with the session', () => {
        const href = getSessionEventsHref('/next/event/all-2', 'session-id');
        const url = new URL(href!, 'https://example.test');

        expect(url.pathname).toBe('/next/event/all-2');
        expect(Object.fromEntries(url.searchParams)).toEqual({
            bot: '',
            filter: '',
            first: '',
            level: '',
            project: '',
            reference: '',
            session: 'session-id',
            stack: '',
            status: '',
            tag: '',
            time: 'all',
            type: '',
            version: ''
        });
    });

    it('does not enable navigation without both a path and session id', () => {
        expect(getSessionEventsHref(undefined, 'session-id')).toBeUndefined();
        expect(getSessionEventsHref('/next/event', undefined)).toBeUndefined();
    });
});
