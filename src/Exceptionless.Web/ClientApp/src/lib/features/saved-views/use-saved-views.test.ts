import { ChangeType } from '$features/websockets/models';
import { QueryClient } from '@tanstack/svelte-query';
import { afterEach, describe, expect, it, vi } from 'vitest';

import type { SavedView } from './models';

import { invalidateSavedViewQueries, queryKeys, removeSavedViewFromCaches, SAVED_VIEW_REFRESH_DELAY_MS, syncSavedViewCaches } from './api.svelte';
import { savedViewHref, savedViewResolvedSlug } from './slugs';
import {
    clearSavedViewQueryParams,
    filterDefinitionsEqual,
    getComparableSavedViewFilter,
    getComparableSavedViewTime,
    getDraftSortValue,
    getSavedViewStateSignature,
    hasMissingSavedView,
    hasSavedViewAutoFillChange,
    hasSavedViewColumnChanges,
    isSavedViewHydrationPending,
    savedViewColumnsEqual,
    type SavedViewQueryParams,
    setSortQueryParam,
    setTimeQueryParam,
    supportsSortQueryParam,
    supportsTimeQueryParam
} from './use-saved-views.svelte';

vi.mock('$features/auth/index.svelte', () => ({
    accessToken: { current: 'token_123' }
}));

const TEST_ORG_ID = '507f1f77bcf86cd799439011';
const TEST_USER_ID = '66a1b2c3d4e5f6a7b8c9d0e1';

afterEach(() => {
    vi.useRealTimers();
});

function buildSavedView({ id, name, ...overrides }: Partial<SavedView> & Pick<SavedView, 'id' | 'name'>): SavedView {
    const slug =
        overrides.slug ??
        name
            .toLowerCase()
            .replace(/[^a-z0-9]+/g, '-')
            .replace(/^-|-$/g, '');

    return {
        columns: {},
        created_by_user_id: TEST_USER_ID,
        created_utc: new Date().toISOString(),
        filter: null,
        filter_definitions: null,
        id,
        name,
        organization_id: TEST_ORG_ID,
        sort: null,
        time: null,
        updated_by_user_id: null,
        updated_utc: new Date().toISOString(),
        user_id: null,
        version: 1,
        view_type: 'stacks',
        ...overrides,
        slug
    };
}

describe('useSavedViews', () => {
    describe('saved view state signatures', () => {
        it('ignores audit and naming changes outside the hydrated state baseline', () => {
            // Arrange
            const savedView = buildSavedView({ id: 'view-1', name: 'Original Name', show_chart: false });
            const renamedView = {
                ...savedView,
                name: 'Renamed View',
                updated_utc: new Date(Date.now() + 1000).toISOString(),
                version: savedView.version + 1
            };

            // Act / Assert
            expect(getSavedViewStateSignature(renamedView)).toBe(getSavedViewStateSignature(savedView));
        });

        it('detects server changes that affect saved view state', () => {
            // Arrange
            const savedView = buildSavedView({
                columns: { summary: { visible: true, wrap: false } },
                id: 'view-1',
                name: 'My View',
                show_chart: true
            });

            // Act / Assert
            expect(getSavedViewStateSignature({ ...savedView, show_chart: false })).not.toBe(getSavedViewStateSignature(savedView));
            expect(
                getSavedViewStateSignature({
                    ...savedView,
                    columns: { summary: { visible: true, wrap: true } }
                })
            ).not.toBe(getSavedViewStateSignature(savedView));
        });
    });

    describe('saved view slugs', () => {
        it('falls back to the normalized name for views created before slugs were stored', () => {
            // Arrange
            const savedView = buildSavedView({ id: 'view-1', name: 'Legacy Saved View', slug: '' });

            // Act
            const slug = savedViewResolvedSlug(savedView);

            // Assert
            expect(slug).toBe('legacy-saved-view');
        });

        it('builds saved-view URLs with the resolved slug', () => {
            // Arrange
            const savedView = buildSavedView({ id: 'view-1', name: 'Legacy Saved View', slug: '', view_type: 'events' });

            // Act
            const href = savedViewHref(savedView);

            // Assert
            expect(href).toBe('/next/event/legacy-saved-view');
        });
    });

    describe('saved view slug resolution', () => {
        it('reports a missing slug after saved views finish loading without a match', () => {
            // Arrange
            const savedView = buildSavedView({ id: 'view-1', name: 'My Saved View' });

            // Act
            const result = hasMissingSavedView({
                activeSavedView: undefined,
                isLoading: false,
                savedViewKey: 'most-frequent',
                savedViews: [savedView]
            });

            // Assert
            expect(result).toBe(true);
        });

        it('reports a missing slug while cached saved-view data is background fetching', () => {
            // Act
            const result = hasMissingSavedView({
                activeSavedView: undefined,
                isLoading: false,
                savedViewKey: 'most-frequent',
                savedViews: []
            });

            // Assert
            expect(result).toBe(true);
        });

        it('does not report a missing slug before saved views are available', () => {
            // Act
            const result = hasMissingSavedView({
                activeSavedView: undefined,
                isLoading: false,
                savedViewKey: 'most-frequent',
                savedViews: undefined
            });

            // Assert
            expect(result).toBe(false);
        });

        it('does not report a missing slug when there is no slug route parameter', () => {
            // Act
            const result = hasMissingSavedView({
                activeSavedView: undefined,
                isLoading: false,
                savedViewKey: undefined,
                savedViews: []
            });

            // Assert
            expect(result).toBe(false);
        });

        it('reports a missing query-selected saved view after loading finishes', () => {
            const result = hasMissingSavedView({
                activeSavedView: undefined,
                isLoading: false,
                savedViewKey: 'view-1',
                savedViews: []
            });

            expect(result).toBe(true);
        });
    });

    describe('saved view hydration readiness', () => {
        it('waits until the selected saved view draft is applied', () => {
            expect(isSavedViewHydrationPending('view-1', undefined, undefined, false)).toBe(true);
            expect(isSavedViewHydrationPending('view-1', 'view-1', undefined, false)).toBe(true);
            expect(isSavedViewHydrationPending('view-1', 'view-1', 'view-1', false)).toBe(false);
        });

        it('does not wait when no view is selected or the selected view is missing', () => {
            expect(isSavedViewHydrationPending(undefined, undefined, undefined, false)).toBe(false);
            expect(isSavedViewHydrationPending('missing', undefined, undefined, true)).toBe(false);
        });
    });

    describe('filter definition comparison', () => {
        it('treats omitted empty filter values as equal to hydrated empty values', () => {
            // Arrange
            const seededDefinitions = '[{"type":"date","term":"date","value":"[now-7d TO now]"},{"type":"project"}]';
            const serializedDefinitions = '[{"type":"date","term":"date","value":"[now-7d TO now]"},{"type":"project","value":[]}]';

            // Act
            const result = filterDefinitionsEqual(serializedDefinitions, seededDefinitions);

            // Assert
            expect(result).toBe(true);
        });

        it('treats equivalent filter definitions in different order as equal', () => {
            // Arrange
            const seededDefinitions =
                '[{"type":"date","term":"date","value":"[now-7d TO now]"},{"type":"project"},{"type":"status","value":["open","regressed"],"hidden":true},{"type":"type","value":["404"],"hidden":true}]';
            const serializedDefinitions =
                '[{"type":"project","value":[]},{"type":"status","value":["open","regressed"],"hidden":true},{"type":"type","value":["404"],"hidden":true},{"type":"date","term":"date","value":"[now-7d TO now]"}]';

            // Act
            const result = filterDefinitionsEqual(serializedDefinitions, seededDefinitions);

            // Assert
            expect(result).toBe(true);
        });

        it('uses the route default filter when saved views do not have filter definitions', () => {
            // Act
            const result = getComparableSavedViewFilter(null, null, '(status:open OR status:regressed)');

            // Assert
            expect(result).toBe('(status:open OR status:regressed)');
        });

        it('does not apply the route default filter when saved filter definitions are present', () => {
            // Act
            const result = getComparableSavedViewFilter(null, '[]', '(status:open OR status:regressed)');

            // Assert
            expect(result).toBeNull();
        });
    });

    describe('time comparison', () => {
        it('uses the route default time when saved views do not store time', () => {
            // Act
            const result = getComparableSavedViewTime(null, '[now-7d TO now]');

            // Assert
            expect(result).toBe('[now-7d TO now]');
        });
    });

    describe('sort drafts', () => {
        it('keeps a one-off URL sort override out of a local draft', () => {
            expect(getDraftSortValue('-date', 'type', { value: 'type' }, undefined)).toBe('-date');
        });

        it('preserves an older local sort behind a one-off URL override', () => {
            expect(getDraftSortValue('-date', 'type', { value: 'type' }, { sort: 'count', version: 1 })).toBe('count');
        });

        it('preserves an older local sort when a URL override matches the server', () => {
            expect(getDraftSortValue('-date', '-date', { value: '-date' }, { sort: 'count', version: 1 })).toBe('count');
        });

        it('persists a sort changed after hydration', () => {
            expect(getDraftSortValue('-date', 'count', { value: 'type' }, undefined)).toBe('count');
        });
    });

    describe('column comparison', () => {
        it('detects a changed auto-fill column', () => {
            const savedView = buildSavedView({
                columns: {
                    date: { visible: true },
                    summary: { auto_fill: true, visible: true }
                },
                id: 'view-1',
                name: 'Summary View'
            });

            expect(hasSavedViewAutoFillChange('date', savedView, 'summary')).toBe(true);
            expect(hasSavedViewAutoFillChange('summary', savedView, 'summary')).toBe(false);
        });

        it('uses the default auto-fill column for legacy saved views', () => {
            const savedView = buildSavedView({ id: 'view-1', name: 'Legacy View' });

            expect(hasSavedViewAutoFillChange('summary', savedView, 'summary')).toBe(false);
            expect(hasSavedViewAutoFillChange('date', savedView, 'summary')).toBe(true);
        });

        it('treats an explicit None selection as unchanged', () => {
            const savedView = buildSavedView({
                columns: {
                    summary: { auto_fill: false, visible: true }
                },
                id: 'view-1',
                name: 'Fixed Width View'
            });

            expect(hasSavedViewAutoFillChange(null, savedView, 'summary')).toBe(false);
            expect(hasSavedViewAutoFillChange('summary', savedView, 'summary')).toBe(true);
        });

        it('treats visibility missing default-hidden columns as unchanged', () => {
            // Arrange
            const current = { project: false, summary: true, tags: false };
            const saved = { summary: true };
            const defaults = { project: false, tags: false };

            // Act
            const result = savedViewColumnsEqual(current, saved, defaults);

            // Assert
            expect(result).toBe(true);
        });

        it('detects a changed column after applying default visibility', () => {
            // Arrange
            const current = { project: true, summary: true, tags: false };
            const saved = { summary: true };
            const defaults = { project: false, tags: false };

            // Act
            const result = savedViewColumnsEqual(current, saved, defaults);

            // Assert
            expect(result).toBe(false);
        });
        it('marks a saved view as changed when adding a column omitted from its settings', () => {
            // Arrange
            const current = { project: true, tags: false };
            const defaults = { project: false, tags: false };

            // Act
            const result = hasSavedViewColumnChanges(current, null, defaults);

            // Assert
            expect(result).toBe(true);
        });

        it('does not mark an unchanged saved view with omitted column settings as changed', () => {
            // Arrange
            const defaults = { project: false, tags: false };

            // Act
            const result = hasSavedViewColumnChanges(defaults, null, defaults);

            // Assert
            expect(result).toBe(false);
        });

        it('does not mark a default-visible column as changed after it is removed and re-added', () => {
            // Arrange
            const current = { project: false, summary: true, tags: false };
            const defaults = { project: false, tags: false };

            // Act
            const result = hasSavedViewColumnChanges(current, null, defaults);

            // Assert
            expect(result).toBe(false);
        });
    });

    describe('time parameter detection', () => {
        it('detects when time is not in query params (stream page)', () => {
            // Arrange
            const queryParamsWithoutTime: SavedViewQueryParams = {
                filter: null,
                saved: undefined
            };

            // Act
            const supportsTime = supportsTimeQueryParam(queryParamsWithoutTime);

            // Assert
            expect(supportsTime).toBe(false);
        });

        it('detects when time is in query params (issues page)', () => {
            // Arrange
            const queryParamsWithTime: SavedViewQueryParams = {
                filter: null,
                saved: undefined,
                time: '[now-7d TO now]'
            };

            // Act
            const supportsTime = supportsTimeQueryParam(queryParamsWithTime);

            // Assert
            expect(supportsTime).toBe(true);
        });

        it('treats time as supported when it exists but is undefined', () => {
            // Arrange
            const queryParamsTimeUndefined: SavedViewQueryParams = {
                filter: null,
                saved: undefined,
                time: undefined
            };

            // Act
            const supportsTime = supportsTimeQueryParam(queryParamsTimeUndefined);

            // Assert
            expect(supportsTime).toBe(true);
        });
    });

    describe('time parameter updates', () => {
        it('does not write time when the route does not support it', () => {
            // Arrange
            const target: SavedViewQueryParams = {
                filter: null,
                saved: undefined
            };
            const queryParams = new Proxy(target, {
                set(obj, prop, value) {
                    if (prop === 'time') {
                        throw new Error(`unexpected time assignment: ${String(value)}`);
                    }

                    return Reflect.set(obj, prop, value);
                }
            }) as SavedViewQueryParams;

            // Act & Assert
            expect(() => {
                setTimeQueryParam(queryParams, null);
            }).not.toThrow();
            expect('time' in target).toBe(false);
        });

        it('updates time when the route supports it', () => {
            // Arrange
            const queryParams: SavedViewQueryParams = {
                filter: null,
                saved: undefined,
                time: undefined
            };

            // Act
            setTimeQueryParam(queryParams, '[now-15m TO now]');

            // Assert
            expect(queryParams.time).toBe('[now-15m TO now]');
        });

        it('clears time when the route supports it', () => {
            // Arrange
            const queryParams: SavedViewQueryParams = {
                filter: null,
                saved: undefined,
                time: '[now-15m TO now]'
            };

            // Act
            setTimeQueryParam(queryParams, null);

            // Assert
            expect(queryParams.time).toBeNull();
        });
    });

    describe('saved parameter clearing', () => {
        it('clears saved view selection to null instead of undefined', () => {
            // Arrange
            const queryParams: SavedViewQueryParams = {
                filter: 'type:error',
                filters: 'type:error',
                saved: 'view-1',
                sort: '-date',
                time: '[now-7d TO now]'
            };

            // Act
            clearSavedViewQueryParams(queryParams);

            // Assert
            expect(queryParams).toEqual({
                filter: null,
                filters: null,
                saved: null,
                sort: null,
                time: null
            });
        });

        it('does not write query parameters unsupported by the route', () => {
            // Arrange
            const target: SavedViewQueryParams = {
                filter: 'type:error',
                time: '[now-7d TO now]'
            };
            const queryParams = new Proxy(target, {
                set(obj, prop, value) {
                    if (prop === 'filters' || prop === 'saved' || prop === 'sort') {
                        throw new Error(`unexpected ${String(prop)} assignment: ${String(value)}`);
                    }

                    return Reflect.set(obj, prop, value);
                }
            }) as SavedViewQueryParams;

            // Act & Assert
            expect(() => {
                clearSavedViewQueryParams(queryParams);
            }).not.toThrow();
            expect(queryParams).toEqual({
                filter: null,
                time: null
            });
        });
    });

    describe('sort parameter detection', () => {
        it('detects when sort is not in query params', () => {
            // Arrange
            const queryParamsWithoutSort: SavedViewQueryParams = {
                filter: null,
                saved: undefined
            };

            // Act
            const supportsSort = supportsSortQueryParam(queryParamsWithoutSort);

            // Assert
            expect(supportsSort).toBe(false);
        });

        it('detects when sort is in query params', () => {
            // Arrange
            const queryParamsWithSort: SavedViewQueryParams = {
                filter: null,
                saved: undefined,
                sort: '-date'
            };

            // Act
            const supportsSort = supportsSortQueryParam(queryParamsWithSort);

            // Assert
            expect(supportsSort).toBe(true);
        });
    });

    describe('sort parameter updates', () => {
        it('does not write sort when the route does not support it', () => {
            // Arrange
            const target: SavedViewQueryParams = {
                filter: null,
                saved: undefined
            };
            const queryParams = new Proxy(target, {
                set(obj, prop, value) {
                    if (prop === 'sort') {
                        throw new Error(`unexpected sort assignment: ${String(value)}`);
                    }

                    return Reflect.set(obj, prop, value);
                }
            }) as SavedViewQueryParams;

            // Act & Assert
            expect(() => {
                setSortQueryParam(queryParams, null);
            }).not.toThrow();
            expect('sort' in target).toBe(false);
        });

        it('updates sort when the route supports it', () => {
            // Arrange
            const queryParams: SavedViewQueryParams = {
                filter: null,
                saved: undefined,
                sort: undefined
            };

            // Act
            setSortQueryParam(queryParams, '-date');

            // Assert
            expect(queryParams.sort).toBe('-date');
        });
    });

    describe('saved view websocket invalidation', () => {
        it('delays invalidation for Added events so optimistic caches stay visible', async () => {
            // Arrange
            vi.useFakeTimers();
            const queryClient = new QueryClient();
            const invalidateSpy = vi.spyOn(queryClient, 'invalidateQueries').mockImplementation(async () => {});

            // Act
            await invalidateSavedViewQueries(queryClient, {
                change_type: ChangeType.Added,
                data: {},
                organization_id: TEST_ORG_ID,
                type: 'SavedView'
            });

            // Assert
            expect(invalidateSpy).not.toHaveBeenCalled();

            await vi.advanceTimersByTimeAsync(SAVED_VIEW_REFRESH_DELAY_MS);
            expect(invalidateSpy).toHaveBeenCalledWith({ queryKey: queryKeys.organization(TEST_ORG_ID) });
        });

        it('delays invalidation for Saved events so optimistic caches stay visible', async () => {
            // Arrange
            vi.useFakeTimers();
            const queryClient = new QueryClient();
            const invalidateSpy = vi.spyOn(queryClient, 'invalidateQueries').mockImplementation(async () => {});

            // Act
            await invalidateSavedViewQueries(queryClient, {
                change_type: ChangeType.Saved,
                data: {},
                organization_id: TEST_ORG_ID,
                type: 'SavedView'
            });

            // Assert
            expect(invalidateSpy).not.toHaveBeenCalled();

            await vi.advanceTimersByTimeAsync(SAVED_VIEW_REFRESH_DELAY_MS);
            expect(invalidateSpy).toHaveBeenCalledWith({ queryKey: queryKeys.organization(TEST_ORG_ID) });
        });

        it('coalesces rapid saved view notifications until the latest refresh window', async () => {
            // Arrange
            vi.useFakeTimers();
            const queryClient = new QueryClient();
            const invalidateSpy = vi.spyOn(queryClient, 'invalidateQueries').mockImplementation(async () => {});

            // Act
            await invalidateSavedViewQueries(queryClient, {
                change_type: ChangeType.Added,
                data: {},
                organization_id: TEST_ORG_ID,
                type: 'SavedView'
            });
            await vi.advanceTimersByTimeAsync(SAVED_VIEW_REFRESH_DELAY_MS - 1);
            await invalidateSavedViewQueries(queryClient, {
                change_type: ChangeType.Added,
                data: {},
                organization_id: TEST_ORG_ID,
                type: 'SavedView'
            });
            await vi.advanceTimersByTimeAsync(1);

            // Assert
            expect(invalidateSpy).not.toHaveBeenCalled();

            await vi.advanceTimersByTimeAsync(SAVED_VIEW_REFRESH_DELAY_MS - 1);
            expect(invalidateSpy).toHaveBeenCalledOnce();
            expect(invalidateSpy).toHaveBeenCalledWith({ queryKey: queryKeys.organization(TEST_ORG_ID) });
        });

        it('removes from cache in-place for Removed events when view is cached', async () => {
            // Arrange
            const queryClient = new QueryClient();
            const view = buildSavedView({ id: 'view-1', name: 'My View' });
            const otherView = buildSavedView({ id: 'view-2', name: 'Other View' });
            queryClient.setQueryData(queryKeys.organization(TEST_ORG_ID), [view, otherView]);
            queryClient.setQueryData(queryKeys.view(TEST_ORG_ID, 'stacks'), [view, otherView]);

            // Act
            await invalidateSavedViewQueries(queryClient, {
                change_type: ChangeType.Removed,
                data: {},
                id: 'view-1',
                organization_id: TEST_ORG_ID,
                type: 'SavedView'
            });

            // Assert - view removed from both caches without refetch
            expect(queryClient.getQueryData<SavedView[]>(queryKeys.organization(TEST_ORG_ID))).toEqual([otherView]);
            expect(queryClient.getQueryData<SavedView[]>(queryKeys.view(TEST_ORG_ID, 'stacks'))).toEqual([otherView]);
        });

        it('preserves a pending reconciliation when a cached view is removed', async () => {
            // Arrange
            vi.useFakeTimers();
            const queryClient = new QueryClient();
            const removedView = buildSavedView({ id: 'view-1', name: 'Removed View' });
            queryClient.setQueryData(queryKeys.organization(TEST_ORG_ID), [removedView]);
            const invalidateSpy = vi.spyOn(queryClient, 'invalidateQueries').mockImplementation(async () => {});

            // Act
            await invalidateSavedViewQueries(queryClient, {
                change_type: ChangeType.Added,
                data: {},
                organization_id: TEST_ORG_ID,
                type: 'SavedView'
            });
            await invalidateSavedViewQueries(queryClient, {
                change_type: ChangeType.Removed,
                data: {},
                id: removedView.id,
                organization_id: TEST_ORG_ID,
                type: 'SavedView'
            });

            // Assert
            expect(queryClient.getQueryData<SavedView[]>(queryKeys.organization(TEST_ORG_ID))).toEqual([]);
            expect(invalidateSpy).not.toHaveBeenCalled();

            await vi.advanceTimersByTimeAsync(SAVED_VIEW_REFRESH_DELAY_MS);
            expect(invalidateSpy).toHaveBeenCalledOnce();
            expect(invalidateSpy).toHaveBeenCalledWith({ queryKey: queryKeys.organization(TEST_ORG_ID) });
        });

        it('falls back to invalidation for Removed events when view is not cached', async () => {
            // Arrange
            const queryClient = new QueryClient();
            const invalidateSpy = vi.spyOn(queryClient, 'invalidateQueries').mockImplementation(async () => {});

            // Act
            await invalidateSavedViewQueries(queryClient, {
                change_type: ChangeType.Removed,
                data: {},
                id: 'view-1',
                organization_id: TEST_ORG_ID,
                type: 'SavedView'
            });

            // Assert - falls through to invalidation since view not in cache
            expect(invalidateSpy).toHaveBeenCalledWith({ queryKey: queryKeys.organization(TEST_ORG_ID) });
        });
    });

    describe('saved view cache helpers', () => {
        it('syncs a created view into both caches immediately', () => {
            // Arrange
            const queryClient = new QueryClient();
            const existingView = buildSavedView({ id: 'view-1', name: 'Existing View' });
            const createdView = buildSavedView({ id: 'view-2', name: 'New View' });

            queryClient.setQueryData(queryKeys.view(TEST_ORG_ID, 'stacks'), [existingView]);
            queryClient.setQueryData(queryKeys.organization(TEST_ORG_ID), [existingView]);

            // Act
            syncSavedViewCaches(queryClient, createdView);

            // Assert
            expect(queryClient.getQueryData<SavedView[]>(queryKeys.view(TEST_ORG_ID, 'stacks'))).toEqual([existingView, createdView]);
            expect(queryClient.getQueryData<SavedView[]>(queryKeys.organization(TEST_ORG_ID))).toEqual([existingView, createdView]);
        });

        it('uses the explicit organization id when syncing a created view', () => {
            // Arrange
            const queryClient = new QueryClient();
            const createdView = buildSavedView({ id: 'view-1', name: 'New View', organization_id: undefined as never });

            // Act
            syncSavedViewCaches(queryClient, createdView, TEST_ORG_ID);

            // Assert
            expect(queryClient.getQueryData<SavedView[]>(queryKeys.view(TEST_ORG_ID, 'stacks'))).toEqual([createdView]);
            expect(queryClient.getQueryData<SavedView[]>(queryKeys.organization(TEST_ORG_ID))).toEqual([createdView]);
        });

        it('syncs an updated view into both caches immediately', () => {
            // Arrange
            const queryClient = new QueryClient();
            const existingView = buildSavedView({ filter: 'type:error', id: 'view-1', name: 'Existing View' });
            const otherView = buildSavedView({ id: 'view-2', name: 'Other View' });
            const updatedView = {
                ...existingView,
                filter: 'type:log',
                time: '[now-15m TO now]'
            };

            queryClient.setQueryData(queryKeys.view(TEST_ORG_ID, 'stacks'), [existingView, otherView]);
            queryClient.setQueryData(queryKeys.organization(TEST_ORG_ID), [existingView, otherView]);

            // Act
            syncSavedViewCaches(queryClient, updatedView);

            // Assert
            expect(queryClient.getQueryData<SavedView[]>(queryKeys.view(TEST_ORG_ID, 'stacks'))).toEqual([updatedView, otherView]);
            expect(queryClient.getQueryData<SavedView[]>(queryKeys.organization(TEST_ORG_ID))).toEqual([updatedView, otherView]);
        });

        it('removes a deleted view from every saved-view list cache', () => {
            // Arrange
            const queryClient = new QueryClient();
            const deletedView = buildSavedView({ id: 'view-1', name: 'Deleted View' });
            const otherView = buildSavedView({ id: 'view-2', name: 'Other View' });

            queryClient.setQueryData(queryKeys.organization(TEST_ORG_ID), [deletedView, otherView]);
            queryClient.setQueryData(queryKeys.view(TEST_ORG_ID, 'stacks'), [deletedView, otherView]);
            queryClient.setQueryData(queryKeys.view(TEST_ORG_ID, 'events'), [deletedView, otherView]);

            // Act
            removeSavedViewFromCaches(queryClient, deletedView, TEST_ORG_ID);

            // Assert
            expect(queryClient.getQueryData<SavedView[]>(queryKeys.organization(TEST_ORG_ID))).toEqual([otherView]);
            expect(queryClient.getQueryData<SavedView[]>(queryKeys.view(TEST_ORG_ID, 'stacks'))).toEqual([otherView]);
            expect(queryClient.getQueryData<SavedView[]>(queryKeys.view(TEST_ORG_ID, 'events'))).toEqual([otherView]);
        });
    });

    describe('rename cache update pattern', () => {
        it('correctly updates the name of a specific view in a list', () => {
            // Arrange
            const views: SavedView[] = [
                buildSavedView({ filter: 'type:error', id: 'view-1', name: 'Old Name' }),
                buildSavedView({ filter: 'type:log', id: 'view-2', name: 'Other View' })
            ];
            const viewId = 'view-1';
            const newName = 'New Name';

            // Act - Pattern used in handleRename optimistic update
            const updateViews = (old: SavedView[] | undefined): SavedView[] | undefined => {
                if (!old) {
                    return old;
                }

                return old.map((v) => (v.id === viewId ? { ...v, name: newName } : v));
            };

            const updated = updateViews(views);

            // Assert
            expect(updated).toBeDefined();
            expect(updated).toHaveLength(2);
            if (updated) {
                expect(updated[0]!.id).toBe('view-1');
                expect(updated[0]!.name).toBe('New Name');
                expect(updated[1]!.id).toBe('view-2');
                expect(updated[1]!.name).toBe('Other View');
            }
        });

        it('handles undefined cache gracefully', () => {
            // Arrange
            const viewId = 'view-1';
            const newName = 'New Name';

            // Act - Pattern used in handleRename optimistic update
            const updateViews = (old: SavedView[] | undefined): SavedView[] | undefined => {
                if (!old) {
                    return old;
                }

                return old.map((v) => (v.id === viewId ? { ...v, name: newName } : v));
            };

            const updated = updateViews(undefined);

            // Assert
            expect(updated).toBeUndefined();
        });
    });
});
