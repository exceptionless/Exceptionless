import { ChangeType } from '$features/websockets/models';
import { QueryClient } from '@tanstack/svelte-query';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';

import type { SavedView } from './models';

import { invalidateSavedViewQueries, queryKeys, syncSavedViewCaches, upsertSavedViewCache } from './api.svelte';
import { type SavedViewQueryParams, setTimeQueryParam, supportsTimeQueryParam } from './use-saved-views.svelte';

function buildSavedView({ id, name, ...overrides }: Partial<SavedView> & Pick<SavedView, 'id' | 'name'>): SavedView {
    return {
        columns: {},
        created_by_user_id: 'user-1',
        created_utc: '2024-01-01T00:00:00Z',
        filter: null,
        filter_definitions: null,
        id,
        is_default: false,
        name,
        organization_id: 'org-123',
        time: null,
        updated_by_user_id: null,
        updated_utc: '2024-01-01T00:00:00Z',
        user_id: null,
        version: 1,
        view: 'issues',
        ...overrides
    };
}

describe('useSavedViews', () => {
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

    describe('saved view websocket invalidation', () => {
        beforeEach(() => {
            vi.useFakeTimers();
        });

        afterEach(() => {
            vi.useRealTimers();
        });

        it('delays Saved websocket invalidation so stale organization data does not overwrite the optimistic cache early', async () => {
            // Arrange
            const queryClient = new QueryClient();
            const staleSavedView = buildSavedView({ filter: 'type:error', id: 'view-1', name: 'Original View' });
            const optimisticSavedView = {
                ...staleSavedView,
                filter: 'type:log',
                name: 'Updated View'
            };

            queryClient.setQueryData(queryKeys.organization('org-123'), [optimisticSavedView]);
            const invalidateSpy = vi.spyOn(queryClient, 'invalidateQueries').mockImplementation(async (filters) => {
                if (filters?.queryKey) {
                    queryClient.setQueryData(filters.queryKey, [staleSavedView]);
                }
            });

            // Act
            const invalidatePromise = invalidateSavedViewQueries(queryClient, {
                change_type: ChangeType.Saved,
                data: {},
                organization_id: 'org-123',
                type: 'SavedView'
            });

            await vi.advanceTimersByTimeAsync(1499);

            // Assert
            expect(invalidateSpy).not.toHaveBeenCalled();
            expect(queryClient.getQueryData<SavedView[]>(queryKeys.organization('org-123'))).toEqual([optimisticSavedView]);

            // Act
            await vi.advanceTimersByTimeAsync(1);
            await invalidatePromise;

            // Assert
            expect(invalidateSpy).toHaveBeenCalledWith({ queryKey: queryKeys.organization('org-123') });
            expect(queryClient.getQueryData<SavedView[]>(queryKeys.organization('org-123'))).toEqual([staleSavedView]);
        });
    });

    describe('saved view cache helpers', () => {
        it('syncs a created view into both caches immediately', () => {
            // Arrange
            const queryClient = new QueryClient();
            const existingView = buildSavedView({ id: 'view-1', name: 'Existing View' });
            const createdView = buildSavedView({ id: 'view-2', name: 'New View' });

            queryClient.setQueryData(queryKeys.view('org-123', 'issues'), [existingView]);
            queryClient.setQueryData(queryKeys.organization('org-123'), [existingView]);

            // Act
            syncSavedViewCaches(queryClient, createdView);

            // Assert
            expect(queryClient.getQueryData<SavedView[]>(queryKeys.view('org-123', 'issues'))).toEqual([existingView, createdView]);
            expect(queryClient.getQueryData<SavedView[]>(queryKeys.organization('org-123'))).toEqual([existingView, createdView]);
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

            queryClient.setQueryData(queryKeys.view('org-123', 'issues'), [existingView, otherView]);
            queryClient.setQueryData(queryKeys.organization('org-123'), [existingView, otherView]);

            // Act
            syncSavedViewCaches(queryClient, updatedView);

            // Assert
            expect(queryClient.getQueryData<SavedView[]>(queryKeys.view('org-123', 'issues'))).toEqual([updatedView, otherView]);
            expect(queryClient.getQueryData<SavedView[]>(queryKeys.organization('org-123'))).toEqual([updatedView, otherView]);
        });

        it('keeps only one default per saved-view type in the cached list', () => {
            // Arrange
            const currentDefault = buildSavedView({ id: 'view-1', is_default: true, name: 'Current Default' });
            const otherIssuesView = buildSavedView({ id: 'view-2', name: 'Other Issues View' });
            const streamDefault = buildSavedView({ id: 'view-3', is_default: true, name: 'Stream Default', view: 'stream' });
            const newDefault = buildSavedView({ id: 'view-4', is_default: true, name: 'New Default' });

            // Act
            const updatedViews = upsertSavedViewCache([currentDefault, otherIssuesView, streamDefault], newDefault);

            // Assert
            expect(updatedViews.filter((view) => view.view === 'issues' && view.is_default)).toEqual([newDefault]);
            expect(updatedViews.filter((view) => view.view === 'stream' && view.is_default)).toEqual([streamDefault]);
        });
    });

    describe('rename cache update pattern', () => {
        it('correctly updates the name of a specific view in a list', () => {
            // Arrange
            const views: SavedView[] = [
                {
                    columns: {},
                    created_by_user_id: 'user-1',
                    created_utc: '2024-01-01T00:00:00Z',
                    filter: 'type:error',
                    filter_definitions: null,
                    id: 'view-1',
                    is_default: false,
                    name: 'Old Name',
                    organization_id: 'org-123',
                    time: null,
                    updated_by_user_id: null,
                    updated_utc: '2024-01-01T00:00:00Z',
                    user_id: null,
                    version: 1,
                    view: 'issues'
                },
                {
                    columns: {},
                    created_by_user_id: 'user-1',
                    created_utc: '2024-01-02T00:00:00Z',
                    filter: 'type:log',
                    filter_definitions: null,
                    id: 'view-2',
                    is_default: false,
                    name: 'Other View',
                    organization_id: 'org-123',
                    time: null,
                    updated_by_user_id: null,
                    updated_utc: '2024-01-02T00:00:00Z',
                    user_id: null,
                    version: 1,
                    view: 'issues'
                }
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
