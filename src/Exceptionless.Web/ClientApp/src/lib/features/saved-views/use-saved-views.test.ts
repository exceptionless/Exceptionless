import { ChangeType } from '$features/websockets/models';
import { QueryClient } from '@tanstack/svelte-query';
import { afterEach, describe, expect, it, vi } from 'vitest';

import type { SavedView } from './models';

import { invalidateSavedViewQueries, queryKeys, removeSavedViewFromCaches, SAVED_VIEW_REFRESH_DELAY_MS, syncSavedViewCaches } from './api.svelte';
import {
    filterDefinitionsEqual,
    getComparableSavedViewFilter,
    getComparableSavedViewTime,
    hasMissingSavedViewSlug,
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
        column_order: null,
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
    describe('saved view slug resolution', () => {
        it('reports a missing slug after saved views finish loading without a match', () => {
            // Arrange
            const savedView = buildSavedView({ id: 'view-1', name: 'My Saved View' });

            // Act
            const result = hasMissingSavedViewSlug({
                activeSavedView: undefined,
                isLoading: false,
                savedViews: [savedView],
                slug: 'most-frequent'
            });

            // Assert
            expect(result).toBe(true);
        });

        it('reports a missing slug while cached saved-view data is background fetching', () => {
            // Act
            const result = hasMissingSavedViewSlug({
                activeSavedView: undefined,
                isLoading: false,
                savedViews: [],
                slug: 'most-frequent'
            });

            // Assert
            expect(result).toBe(true);
        });

        it('does not report a missing slug before saved views are available', () => {
            // Act
            const result = hasMissingSavedViewSlug({
                activeSavedView: undefined,
                isLoading: false,
                savedViews: undefined,
                slug: 'most-frequent'
            });

            // Assert
            expect(result).toBe(false);
        });

        it('does not report a missing slug when there is no slug route parameter', () => {
            // Act
            const result = hasMissingSavedViewSlug({
                activeSavedView: undefined,
                isLoading: false,
                savedViews: [],
                slug: undefined
            });

            // Assert
            expect(result).toBe(false);
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
