import { KeywordFilter, ProjectFilter, StatusFilter } from '$features/events/components/filters';
import { StackStatus } from '$features/stacks/models';
import { describe, expect, it } from 'vitest';

import type { SavedViewDraftIdentity } from './saved-view-drafts';

import {
    applyFilterChanges,
    applyRecordChanges,
    applyWrappedColumnChanges,
    buildFilterChanges,
    buildFilterOverrideBaselines,
    buildRecordChanges,
    buildWrappedColumnChanges,
    clearSavedViewDraft,
    getMatchingFilterOverrideKeys,
    getSavedViewDraft,
    mergeFilterOverrides,
    mergePendingSavedViewDraftEdits,
    saveSavedViewDraft
} from './saved-view-drafts';

function createStorage() {
    const values = new Map<string, string>();

    return {
        getItem: (key: string) => values.get(key) ?? null,
        removeItem: (key: string) => values.delete(key),
        setItem: (key: string, value: string) => values.set(key, value)
    };
}

const identity: SavedViewDraftIdentity = {
    organizationId: 'organization-1',
    savedViewId: 'view-1',
    userId: 'user-1'
};

describe('saved view drafts', () => {
    it('round trips semantic view changes', () => {
        const storage = createStorage();
        saveSavedViewDraft(
            identity,
            {
                autoFillColumnId: 'summary',
                columnOrder: ['date', 'summary'],
                columnSizingChanges: { date: null, summary: 480 },
                columnVisibilityChanges: { date: false },
                filterChanges: {
                    baselineDefinitions: '[{"type":"keyword","value":"old"}]',
                    duplicateKeys: ['keyword'],
                    removedDefinitions: '[{"type":"keyword","value":"old"}]',
                    removedKeys: ['status'],
                    upsertDefinitions: '[{"type":"project","value":["project-1"]}]'
                },
                showChart: false,
                sort: '-date',
                version: 1,
                wrappedColumnChanges: { summary: true }
            },
            storage
        );

        expect(getSavedViewDraft(identity, storage)).toEqual({
            autoFillColumnId: 'summary',
            columnOrder: ['date', 'summary'],
            columnSizingChanges: { date: null, summary: 480 },
            columnVisibilityChanges: { date: false },
            filterChanges: {
                baselineDefinitions: '[{"type":"keyword","value":"old"}]',
                duplicateKeys: ['keyword'],
                removedDefinitions: '[{"type":"keyword","value":"old"}]',
                removedKeys: ['status'],
                upsertDefinitions: '[{"type":"project","value":["project-1"]}]'
            },
            showChart: false,
            sort: '-date',
            version: 1,
            wrappedColumnChanges: { summary: true }
        });
    });

    it('applies local filter changes over unrelated changes from the latest server view', () => {
        const originalServerFilters = [new ProjectFilter(['project-1']), new StatusFilter([StackStatus.Open])];
        const locallyEditedFilters = [new ProjectFilter(['project-2']), new StatusFilter([StackStatus.Open])];
        const changes = buildFilterChanges(originalServerFilters, locallyEditedFilters);
        const latestServerFilters = [new ProjectFilter(['project-1']), new StatusFilter([StackStatus.Regressed])];

        const mergedFilters = applyFilterChanges(latestServerFilters, changes);

        expect((mergedFilters.find((filter) => filter.key === 'project') as ProjectFilter).value).toEqual(['project-2']);
        expect((mergedFilters.find((filter) => filter.key === 'status') as StatusFilter).value).toEqual([StackStatus.Regressed]);
    });

    it('preserves duplicate keyword filters while rebasing local changes', () => {
        const originalServerFilters = [new KeywordFilter('foo'), new KeywordFilter('bar')];
        const locallyEditedFilters = [new KeywordFilter('foo'), new KeywordFilter('baz')];
        const changes = buildFilterChanges(originalServerFilters, locallyEditedFilters);
        const latestServerFilters = [new KeywordFilter('foo'), new KeywordFilter('bar'), new KeywordFilter('remote')];

        const mergedFilters = applyFilterChanges(latestServerFilters, changes);

        expect(changes?.duplicateKeys).toEqual(['keyword']);
        expect(mergedFilters.map((filter) => (filter as KeywordFilter).value)).toEqual(['foo', 'remote', 'baz']);
    });

    it('does not append a duplicate upsert that the server independently adopted', () => {
        const originalServerFilters = [new KeywordFilter('foo'), new KeywordFilter('bar')];
        const locallyEditedFilters = [new KeywordFilter('bar'), new KeywordFilter('baz')];
        const changes = buildFilterChanges(originalServerFilters, locallyEditedFilters);
        const latestServerFilters = [new KeywordFilter('foo'), new KeywordFilter('bar'), new KeywordFilter('baz')];

        const mergedFilters = applyFilterChanges(latestServerFilters, changes);

        expect(mergedFilters.map((filter) => (filter as KeywordFilter).value)).toEqual(['bar', 'baz']);
    });

    it('preserves a remote duplicate when a formerly unique definition was removed locally', () => {
        const originalServerFilters = [new KeywordFilter('foo')];
        const changes = buildFilterChanges(originalServerFilters, []);
        const latestServerFilters = [new KeywordFilter('foo'), new KeywordFilter('remote')];

        const mergedFilters = applyFilterChanges(latestServerFilters, changes);

        expect(changes?.removedKeys).toEqual([]);
        expect(changes?.removedDefinitions).toBe('[{"type":"keyword","value":"foo"}]');
        expect(mergedFilters.map((filter) => (filter as KeywordFilter).value)).toEqual(['remote']);
    });

    it('continues to apply key removals from legacy browser-local drafts', () => {
        const mergedFilters = applyFilterChanges([new StatusFilter([StackStatus.Open])], {
            removedKeys: ['status'],
            upsertDefinitions: '[]'
        });

        expect(mergedFilters).toEqual([]);
    });

    it('preserves an intentional duplicate beyond the server baseline count', () => {
        const originalServerFilters = [new KeywordFilter('foo')];
        const locallyEditedFilters = [new KeywordFilter('foo'), new KeywordFilter('foo')];
        const changes = buildFilterChanges(originalServerFilters, locallyEditedFilters);

        const mergedFilters = applyFilterChanges(originalServerFilters, changes);

        expect(mergedFilters.map((filter) => (filter as KeywordFilter).value)).toEqual(['foo', 'foo']);
    });

    it('does not repeat a duplicate removal that the server independently adopted', () => {
        const originalServerFilters = [new KeywordFilter('foo'), new KeywordFilter('foo')];
        const locallyEditedFilters = [new KeywordFilter('foo')];
        const changes = buildFilterChanges(originalServerFilters, locallyEditedFilters);
        const latestServerFilters = [new KeywordFilter('foo')];

        const mergedFilters = applyFilterChanges(latestServerFilters, changes);

        expect(mergedFilters.map((filter) => (filter as KeywordFilter).value)).toEqual(['foo']);
    });

    it('keeps a local duplicate addition when the server removes the baseline definition', () => {
        const originalServerFilters = [new KeywordFilter('foo')];
        const locallyEditedFilters = [new KeywordFilter('foo'), new KeywordFilter('foo')];
        const changes = buildFilterChanges(originalServerFilters, locallyEditedFilters);

        const mergedFilters = applyFilterChanges([], changes);

        expect(mergedFilters.map((filter) => (filter as KeywordFilter).value)).toEqual(['foo']);
    });

    it('lets explicit filter keys override a draft without dropping unrelated draft filters', () => {
        const draftFilters = [new ProjectFilter(['saved-project']), new StatusFilter([StackStatus.Regressed])];
        const currentUrlFilters = [new ProjectFilter(['url-project']), new StatusFilter([StackStatus.Open])];

        const mergedFilters = mergeFilterOverrides(draftFilters, currentUrlFilters, ['project']);

        expect((mergedFilters.find((filter) => filter.key === 'project') as ProjectFilter).value).toEqual(['url-project']);
        expect((mergedFilters.find((filter) => filter.key === 'status') as StatusFilter).value).toEqual([StackStatus.Regressed]);
    });

    it('overlays pending non-URL edits without losing stored draft state', () => {
        const merged = mergePendingSavedViewDraftEdits(
            {
                columnSizingChanges: { summary: 480 },
                columnVisibilityChanges: { date: false },
                filterChanges: { removedKeys: [], upsertDefinitions: '[{"type":"project","value":["project-1"]}]' },
                showChart: false,
                sort: 'count',
                version: 1
            },
            {
                columnSizingChanges: { date: 200 },
                columnVisibilityChanges: { summary: false },
                showChart: true,
                showStats: false,
                version: 1
            }
        );

        expect(merged).toEqual({
            columnSizingChanges: { date: 200, summary: 480 },
            columnVisibilityChanges: { date: false, summary: false },
            filterChanges: { removedKeys: [], upsertDefinitions: '[{"type":"project","value":["project-1"]}]' },
            showChart: true,
            showStats: false,
            sort: 'count',
            version: 1
        });
    });

    it('retires an initial filter override after its value changes', () => {
        const initialFilters = [new ProjectFilter(['url-project']), new StatusFilter([StackStatus.Regressed])];
        const baselines = buildFilterOverrideBaselines(initialFilters, ['project']);

        expect(getMatchingFilterOverrideKeys(initialFilters, baselines)).toEqual(['project']);
        expect(getMatchingFilterOverrideKeys([new ProjectFilter(['edited-project']), new StatusFilter([StackStatus.Regressed])], baselines)).toEqual([]);
        expect(getMatchingFilterOverrideKeys([new StatusFilter([StackStatus.Regressed])], baselines)).toEqual([]);
    });

    it('applies per-column changes over the latest server configuration', () => {
        const sizingChanges = buildRecordChanges({ date: 160, summary: 400 }, { summary: 480 });
        const wrappingChanges = buildWrappedColumnChanges(['date'], ['date', 'summary']);

        expect(applyRecordChanges({ date: 200, summary: 400, type: 120 }, sizingChanges)).toEqual({ summary: 480, type: 120 });
        expect(applyWrappedColumnChanges(['date', 'type'], wrappingChanges)).toEqual(['date', 'type', 'summary']);
    });

    it('isolates drafts by user, organization, and saved view', () => {
        const storage = createStorage();
        saveSavedViewDraft(identity, { showChart: false, version: 1 }, storage);

        expect(getSavedViewDraft({ ...identity, userId: 'user-2' }, storage)).toBeUndefined();
        expect(getSavedViewDraft({ ...identity, organizationId: 'organization-2' }, storage)).toBeUndefined();
        expect(getSavedViewDraft({ ...identity, savedViewId: 'view-2' }, storage)).toBeUndefined();
    });

    it('ignores malformed or unsupported drafts', () => {
        const storage = createStorage();
        storage.setItem('exceptionless:saved-view-draft:v1:user-1:organization-1:view-1', JSON.stringify({ showChart: 'no', version: 1 }));

        expect(getSavedViewDraft(identity, storage)).toBeUndefined();
    });

    it('clears a draft after reset or save', () => {
        const storage = createStorage();
        saveSavedViewDraft(identity, { showStats: false, version: 1 }, storage);

        clearSavedViewDraft(identity, storage);

        expect(getSavedViewDraft(identity, storage)).toBeUndefined();
    });
});
