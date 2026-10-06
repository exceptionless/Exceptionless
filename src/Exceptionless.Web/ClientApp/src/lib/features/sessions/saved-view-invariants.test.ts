import type { SavedView } from '$features/saved-views/models';

import { describe, expect, it } from 'vitest';

import { getSessionQueryFilter, normalizeSessionSavedView, SESSION_EVENT_FILTER } from './saved-view-invariants';

function buildSavedView(overrides: Partial<SavedView> = {}): SavedView {
    return {
        columns: {},
        created_by_user_id: '507f1f77bcf86cd799439011',
        created_utc: new Date().toISOString(),
        filter: null,
        filter_definitions: null,
        id: '507f1f77bcf86cd799439012',
        name: 'Sessions',
        organization_id: '507f1f77bcf86cd799439013',
        slug: 'sessions',
        sort: '-date',
        time: '[now-7d TO now]',
        updated_by_user_id: null,
        updated_utc: new Date().toISOString(),
        user_id: null,
        version: 1,
        view_type: 'sessions',
        ...overrides
    };
}

describe('Sessions saved-view invariants', () => {
    it('applies the session event predicate independently of editable filters', () => {
        expect(getSessionQueryFilter(null)).toBe(SESSION_EVENT_FILTER);
        expect(getSessionQueryFilter('project:abc')).toBe('type:session AND (project:abc)');
    });

    it('removes a structured Type filter and recomputes the editable filter', () => {
        const view = buildSavedView({
            filter: 'project:abc type:error',
            filter_definitions: JSON.stringify([
                { type: 'project', value: ['abc'] },
                { hidden: true, type: 'type', value: ['error'] },
                { term: 'date', type: 'date', value: '[now-7d TO now]' }
            ])
        });

        const normalized = normalizeSessionSavedView(view);

        expect(normalized.filter).toBe('project:abc');
        expect(JSON.parse(normalized.filter_definitions ?? '[]')).toEqual([
            { type: 'project', value: ['abc'] },
            { term: 'date', type: 'date', value: '[now-7d TO now]' }
        ]);
    });

    it('removes the legacy raw session-only predicate', () => {
        const normalized = normalizeSessionSavedView(buildSavedView({ filter: ' TYPE:SESSION ' }));

        expect(normalized.filter).toBeNull();
        expect(normalized.filter_definitions).toBeNull();
    });

    it('preserves an advanced raw filter that is not the route invariant', () => {
        const view = buildSavedView({ filter: 'type:error' });

        expect(normalizeSessionSavedView(view)).toBe(view);
    });
});
