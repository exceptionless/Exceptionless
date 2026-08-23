import type { WorkInProgressResult } from '$features/shared/models';
import type { WebSocketMessageValue } from '$features/websockets/models';

import { accessToken } from '$features/auth/index.svelte';
import { ChangeType } from '$features/websockets/models';
import { type ProblemDetails, useFetchClient } from '@foundatiofx/fetchclient';
import { createMutation, createQuery, type QueryClient, useQueryClient } from '@tanstack/svelte-query';

import type { NewSavedView, SavedView, UpdateSavedView, UpdateSavedViewDefault, ViewSavedViewDefaults } from './models';

export const SAVED_VIEW_REFRESH_DELAY_MS = 1500;
export const SAVED_VIEW_QUERY_STALE_TIME_MS = 60 * 1000;

const savedViewInvalidationTimers = new WeakMap<QueryClient, Record<string, ReturnType<typeof setTimeout>>>();

export async function invalidateSavedViewQueries(queryClient: QueryClient, message: WebSocketMessageValue<'SavedViewChanged'>) {
    const { change_type, id, organization_id } = message;

    // Removals: evict from cache immediately without a refetch.
    if (change_type === ChangeType.Removed) {
        if (id && organization_id) {
            const cached = queryClient.getQueryData<SavedView[]>(queryKeys.organization(organization_id));
            const savedView = cached?.find((v) => v.id === id);
            if (savedView) {
                removeSavedViewFromCaches(queryClient, savedView, organization_id);
                return;
            }
        }

        await invalidateSavedViewCache(queryClient, organization_id);
        return;
    }

    // Added/Saved websocket events can arrive before Elasticsearch refresh exposes the
    // saved view to list queries. Mutations already seed the cache, so keep that optimistic
    // item visible and coalesce refetches until the refresh window after the latest event.
    if (change_type === ChangeType.Added || change_type === ChangeType.Saved) {
        scheduleSavedViewInvalidation(queryClient, organization_id);
        return;
    }

    cancelScheduledSavedViewInvalidation(queryClient, organization_id);
    await Promise.all([invalidateSavedViewCache(queryClient, organization_id), invalidateSavedViewDefaultQueries(queryClient, organization_id)]);
}

function cancelScheduledSavedViewInvalidation(queryClient: QueryClient, organizationId: string | undefined) {
    const timers = savedViewInvalidationTimers.get(queryClient);
    const key = organizationId ?? '';
    const timer = timers?.[key];
    if (timers && timer !== undefined) {
        clearTimeout(timer);
        delete timers[key];
    }
}

async function invalidateSavedViewCache(queryClient: QueryClient, organizationId: string | undefined) {
    if (organizationId) {
        await queryClient.invalidateQueries({
            queryKey: queryKeys.organization(organizationId)
        });
    } else {
        await queryClient.invalidateQueries({
            queryKey: queryKeys.type
        });
    }
}

function scheduleSavedViewInvalidation(queryClient: QueryClient, organizationId: string | undefined) {
    cancelScheduledSavedViewInvalidation(queryClient, organizationId);

    const timers = savedViewInvalidationTimers.get(queryClient) ?? {};
    savedViewInvalidationTimers.set(queryClient, timers);

    const key = organizationId ?? '';
    timers[key] = setTimeout(() => {
        delete timers[key];
        void Promise.all([invalidateSavedViewCache(queryClient, organizationId), invalidateSavedViewDefaultQueries(queryClient, organizationId)]);
    }, SAVED_VIEW_REFRESH_DELAY_MS);
}

export const queryKeys = {
    defaults: (organizationId: string | undefined) => [...queryKeys.type, 'organization', organizationId, 'defaults'] as const,
    id: (id: string | undefined) => [...queryKeys.type, id] as const,
    organization: (organizationId: string | undefined) => [...queryKeys.type, 'organization', organizationId] as const,
    predefined: (organizationId: string | undefined) => [...queryKeys.type, 'organization', organizationId, 'predefined'] as const,
    type: ['SavedView'] as const,
    view: (organizationId: string | undefined, view: string | undefined) => [...queryKeys.type, 'organization', organizationId, 'view', view] as const
};

export async function invalidateSavedViewDefaultQueries(queryClient: QueryClient, organizationId: string | undefined) {
    if (organizationId) {
        await queryClient.invalidateQueries({
            queryKey: queryKeys.defaults(organizationId)
        });
        return;
    }

    await queryClient.invalidateQueries({
        predicate: (query) => query.queryKey[0] === queryKeys.type[0] && query.queryKey.at(-1) === 'defaults'
    });
}

let deletedSavedViewIds = $state<string[]>([]);

export function deletePredefinedSavedView(request: { route: { id: string | undefined } }) {
    return createMutation<void, ProblemDetails, void>(() => ({
        enabled: () => !!accessToken.current && !!request.route.id,
        mutationFn: async () => {
            const client = useFetchClient();
            await client.delete(`saved-views/${request.route.id}/predefined`, {
                expectedStatusCodes: [204]
            });
        }
    }));
}

export function deleteSavedView(request: { route: { organizationId: string | undefined } }) {
    const queryClient = useQueryClient();

    return createMutation<WorkInProgressResult, ProblemDetails, SavedView>(() => ({
        enabled: () => !!accessToken.current && !!request.route.organizationId,
        mutationFn: async (savedView: SavedView) => {
            const client = useFetchClient();
            const response = await client.deleteJSON<WorkInProgressResult>(`saved-views/${savedView.id}`);
            return response.data!;
        },
        onError: (_error: ProblemDetails, savedView: SavedView) => {
            restoreDeletedSavedView(savedView);
            syncSavedViewCaches(queryClient, savedView, request.route.organizationId);
        },
        onMutate: (savedView: SavedView) => {
            markSavedViewDeleted(savedView);
            removeSavedViewFromCaches(queryClient, savedView, request.route.organizationId);
        },
        onSettled: () => {
            void queryClient.invalidateQueries({
                queryKey: queryKeys.type
            });
        },
        onSuccess: (_data: WorkInProgressResult, savedView: SavedView) => {
            removeSavedViewFromCaches(queryClient, savedView, request.route.organizationId);
        }
    }));
}

export function getSavedViewDefaultsQuery(request: { route: { organizationId: string | undefined } }) {
    return createQuery<ViewSavedViewDefaults, ProblemDetails>(() => ({
        enabled: () => !!accessToken.current && !!request.route.organizationId,
        queryFn: async () => {
            const client = useFetchClient();
            const response = await client.getJSON<ViewSavedViewDefaults>(`organizations/${request.route.organizationId}/saved-view-defaults`);
            return response.data!;
        },
        queryKey: queryKeys.defaults(request.route.organizationId),
        staleTime: SAVED_VIEW_QUERY_STALE_TIME_MS
    }));
}

// Cacheable reads intentionally finish after their observer unmounts so navigation can reuse the result instead of aborting and restarting the request.
export function getSavedViewsByViewQuery(request: { route: { organizationId: string | undefined; view: string | undefined } }) {
    return createQuery<SavedView[], ProblemDetails>(() => ({
        enabled: () => !!accessToken.current && !!request.route.organizationId && !!request.route.view,
        queryFn: async () => {
            const client = useFetchClient();
            const response = await client.getJSON<SavedView[]>(`organizations/${request.route.organizationId}/saved-views/${request.route.view}`);
            return response.data!;
        },
        queryKey: queryKeys.view(request.route.organizationId, request.route.view),
        staleTime: SAVED_VIEW_QUERY_STALE_TIME_MS
    }));
}

export function getSavedViewsQuery(request: { route: { organizationId: string | undefined } }) {
    return createQuery<SavedView[], ProblemDetails>(() => ({
        enabled: () => !!accessToken.current && !!request.route.organizationId,
        queryFn: async () => {
            const client = useFetchClient();
            const response = await client.getJSON<SavedView[]>(`organizations/${request.route.organizationId}/saved-views`);
            return response.data!;
        },
        queryKey: queryKeys.organization(request.route.organizationId),
        staleTime: SAVED_VIEW_QUERY_STALE_TIME_MS
    }));
}

export function isSavedViewDeleted(savedView: SavedView): boolean {
    return !!savedView.id && deletedSavedViewIds.includes(savedView.id);
}

export function markSavedViewDeleted(savedView: SavedView): void {
    if (savedView.id && !deletedSavedViewIds.includes(savedView.id)) {
        deletedSavedViewIds = [...deletedSavedViewIds, savedView.id];
    }
}

export function patchSavedView(request: { route: { id: string | undefined } }) {
    const queryClient = useQueryClient();

    return createMutation<SavedView, ProblemDetails, UpdateSavedView>(() => ({
        enabled: () => !!accessToken.current && !!request.route.id,
        mutationFn: async (data: UpdateSavedView) => {
            const client = useFetchClient();
            const response = await client.patchJSON<SavedView>(`saved-views/${request.route.id}`, data);
            return response.data!;
        },
        onSuccess: (savedView: SavedView) => {
            syncSavedViewCaches(queryClient, savedView);
        }
    }));
}

export function postPredefinedSavedView(request: { route: { id: string | undefined } }) {
    return createMutation<SavedView, ProblemDetails, void>(() => ({
        enabled: () => !!accessToken.current && !!request.route.id,
        mutationFn: async () => {
            const client = useFetchClient();
            const response = await client.postJSON<SavedView>(`saved-views/${request.route.id}/predefined`, {});
            return response.data!;
        }
    }));
}

export function postPredefinedSavedViews(request: { route: { organizationId: string | undefined } }) {
    const queryClient = useQueryClient();

    return createMutation<SavedView[], ProblemDetails, void>(() => ({
        enabled: () => !!accessToken.current && !!request.route.organizationId,
        mutationFn: async () => {
            const client = useFetchClient();
            const response = await client.postJSON<SavedView[]>(`organizations/${request.route.organizationId}/saved-views/predefined`, {});
            return response.data!;
        },
        mutationKey: queryKeys.predefined(request.route.organizationId),
        onSuccess: (savedViews: SavedView[]) => {
            for (const savedView of savedViews) {
                restoreDeletedSavedView(savedView);
                syncSavedViewCaches(queryClient, savedView, request.route.organizationId);
            }

            void queryClient.invalidateQueries({
                queryKey: queryKeys.organization(request.route.organizationId)
            });
            const viewTypes = savedViews.map((savedView) => savedView.view_type).filter((view, index, views) => views.indexOf(view) === index);
            for (const view of viewTypes) {
                void queryClient.invalidateQueries({
                    queryKey: queryKeys.view(request.route.organizationId, view)
                });
            }
        }
    }));
}

export function postSavedView(request: { route: { organizationId: string | undefined } }) {
    const queryClient = useQueryClient();

    return createMutation<SavedView, ProblemDetails, NewSavedView>(() => ({
        enabled: () => !!accessToken.current && !!request.route.organizationId,
        mutationFn: async (data: NewSavedView) => {
            const client = useFetchClient();
            const response = await client.postJSON<SavedView>(`organizations/${request.route.organizationId}/saved-views`, data);
            return response.data!;
        },
        onSuccess: (savedView: SavedView) => {
            syncSavedViewCaches(queryClient, savedView, request.route.organizationId);
        }
    }));
}

export function putOrganizationSavedViewDefault(request: { route: { organizationId: string | undefined } }) {
    return putSavedViewDefault(request, 'organization');
}

export function putUserSavedViewDefault(request: { route: { organizationId: string | undefined } }) {
    return putSavedViewDefault(request, 'user');
}

export function removeSavedViewFromCaches(queryClient: QueryClient, savedView: SavedView, organizationId: string | undefined = savedView.organization_id) {
    const evict = (cachedViews: SavedView[] | undefined) => cachedViews?.filter((v) => v.id !== savedView.id);
    queryClient.setQueryData(queryKeys.view(organizationId, savedView.view_type), evict);
    queryClient.setQueryData(queryKeys.organization(organizationId), evict);
    queryClient.setQueriesData<SavedView[]>(
        {
            predicate: (query) =>
                query.queryKey[0] === queryKeys.type[0] &&
                query.queryKey[1] === 'organization' &&
                query.queryKey[2] === organizationId &&
                query.queryKey[3] === 'view'
        },
        evict
    );
}

export function restoreDeletedSavedView(savedView: SavedView): void {
    if (savedView.id) {
        deletedSavedViewIds = deletedSavedViewIds.filter((id) => id !== savedView.id);
    }
}

export function syncSavedViewCaches(queryClient: QueryClient, savedView: SavedView, organizationId: string | undefined = savedView.organization_id) {
    queryClient.setQueryData(queryKeys.view(organizationId, savedView.view_type), (cachedViews: SavedView[] | undefined) =>
        upsertSavedViewCache(cachedViews, savedView)
    );
    queryClient.setQueryData(queryKeys.organization(organizationId), (cachedViews: SavedView[] | undefined) => upsertSavedViewCache(cachedViews, savedView));
}

export function upsertSavedViewCache(cachedViews: SavedView[] | undefined, savedView: SavedView): SavedView[] {
    const views = cachedViews ?? [];
    const savedViewIndex = views.findIndex((view) => view.id === savedView.id);

    if (savedViewIndex === -1) {
        return [...views, savedView];
    }

    return views.map((view) => (view.id === savedView.id ? savedView : view));
}

function putSavedViewDefault(request: { route: { organizationId: string | undefined } }, scope: 'organization' | 'user') {
    const queryClient = useQueryClient();

    return createMutation<ViewSavedViewDefaults, ProblemDetails, UpdateSavedViewDefault>(() => ({
        enabled: () => !!accessToken.current && !!request.route.organizationId,
        mutationFn: async (data: UpdateSavedViewDefault) => {
            const client = useFetchClient();
            const response = await client.putJSON<ViewSavedViewDefaults>(`organizations/${request.route.organizationId}/saved-view-defaults/${scope}`, data);
            return response.data!;
        },
        onSuccess: (defaults: ViewSavedViewDefaults) => {
            queryClient.setQueryData(queryKeys.defaults(request.route.organizationId), defaults);
        }
    }));
}
