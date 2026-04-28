import { ChangeType } from '$features/websockets/models';
import { QueryClient } from '@tanstack/svelte-query';
import { describe, expect, it, vi } from 'vitest';

import type { SavedView } from './models';

import { invalidateSavedViewQueries, queryKeys, syncSavedViewCaches, upsertSavedViewCache } from './api.svelte';
import { type SavedViewQueryParams, setTimeQueryParam, supportsTimeQueryParam } from './use-saved-views.svelte';

const TEST_ORG_ID = '507f1f77bcf86cd799439011';
const TEST_USER_ID = '66a1b2c3d4e5f6a7b8c9d0e1';

function buildSavedView({ id, name, ...overrides }: Partial<SavedView> & Pick<SavedView, 'id' | 'name'>): SavedView {
    return {
        columns: {},
        created_by_user_id: TEST_USER_ID,
        created_utc: new Date().toISOString(),
        filter: null,
        filter_definitions: null,
        id,
        is_default: false,
        name,
        organization_id: TEST_ORG_ID,
        time: null,
        updated_by_user_id: null,
        updated_utc: new Date().toISOString(),
        user_id: null,
        version: 1,
        view_type: 'issues',
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
        it('invalidates organization cache immediately on Saved event', async () => {
            // Arrange
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
            expect(invalidateSpy).toHaveBeenCalledWith({ queryKey: queryKeys.organization(TEST_ORG_ID) });
        });
    });

    describe('saved view cache helpers', () => {
        it('syncs a created view into both caches immediately', () => {
            // Arrange
            const queryClient = new QueryClient();
            const existingView = buildSavedView({ id: 'view-1', name: 'Existing View' });
            const createdView = buildSavedView({ id: 'view-2', name: 'New View' });

            queryClient.setQueryData(queryKeys.view(TEST_ORG_ID, 'issues'), [existingView]);
            queryClient.setQueryData(queryKeys.organization(TEST_ORG_ID), [existingView]);

            // Act
            syncSavedViewCaches(queryClient, createdView);

            // Assert
            expect(queryClient.getQueryData<SavedView[]>(queryKeys.view(TEST_ORG_ID, 'issues'))).toEqual([existingView, createdView]);
            expect(queryClient.getQueryData<SavedView[]>(queryKeys.organization(TEST_ORG_ID))).toEqual([existingView, createdView]);
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

            queryClient.setQueryData(queryKeys.view(TEST_ORG_ID, 'issues'), [existingView, otherView]);
            queryClient.setQueryData(queryKeys.organization(TEST_ORG_ID), [existingView, otherView]);

            // Act
            syncSavedViewCaches(queryClient, updatedView);

            // Assert
            expect(queryClient.getQueryData<SavedView[]>(queryKeys.view(TEST_ORG_ID, 'issues'))).toEqual([updatedView, otherView]);
            expect(queryClient.getQueryData<SavedView[]>(queryKeys.organization(TEST_ORG_ID))).toEqual([updatedView, otherView]);
        });

        it('keeps only one default per saved-view type in the cached list', () => {
            // Arrange
            const currentDefault = buildSavedView({ id: 'view-1', is_default: true, name: 'Current Default' });
            const otherIssuesView = buildSavedView({ id: 'view-2', name: 'Other Issues View' });
            const streamDefault = buildSavedView({ id: 'view-3', is_default: true, name: 'Stream Default', view_type: 'stream' });
            const newDefault = buildSavedView({ id: 'view-4', is_default: true, name: 'New Default' });

            // Act
            const updatedViews = upsertSavedViewCache([currentDefault, otherIssuesView, streamDefault], newDefault);

            // Assert
            expect(updatedViews.filter((view) => view.view_type === 'issues' && view.is_default)).toEqual([newDefault]);
            expect(updatedViews.filter((view) => view.view_type === 'stream' && view.is_default)).toEqual([streamDefault]);
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
