import type { WebSocketMessageValue } from '$features/websockets/models';

import { accessToken } from '$features/auth/index.svelte';
import { ChangeType } from '$features/websockets/models';
import { type ProblemDetails, useFetchClient } from '@exceptionless/fetchclient';
import { createMutation, createQuery, type QueryClient, useQueryClient } from '@tanstack/svelte-query';

import type { NewSavedView, SavedView, UpdateSavedView } from './models';

export async function invalidateSavedViewQueries(queryClient: QueryClient, message: WebSocketMessageValue<'SavedViewChanged'>) {
    const { change_type, id, organization_id } = message;

    console.debug('[SavedViewChanged] WebSocket message received', { change_type, id, organization_id });

    // For removals, evict from cache in-place (no refetch needed).
    if (change_type === ChangeType.Removed && id && organization_id) {
        const cached = queryClient.getQueryData<SavedView[]>(queryKeys.organization(organization_id));
        const savedView = cached?.find((v) => v.id === id);
        if (savedView) {
            console.debug('[SavedViewChanged] Evicting removed view from cache', { id });
            removeSavedViewFromCaches(queryClient, savedView, organization_id);
            return;
        }
    }

    // For Added/Saved, mutations already updated the cache optimistically via
    // syncSavedViewCaches. Delay the invalidation by 1500ms so Elasticsearch has
    // time to refresh before the refetch, preventing stale data from overwriting
    // the optimistic update (e.g. a rename appearing to revert after the WS message).
    console.debug('[SavedViewChanged] Scheduling delayed invalidation for Added/Saved', { change_type });
    setTimeout(async () => {
        console.debug('[SavedViewChanged] Running delayed invalidation', { organization_id });
        if (organization_id) {
            await queryClient.invalidateQueries({ queryKey: queryKeys.organization(organization_id) });
        } else {
            await queryClient.invalidateQueries({ queryKey: queryKeys.type });
        }
    }, 1500);
}

export const queryKeys = {
    id: (id: string | undefined) => [...queryKeys.type, id] as const,
    organization: (organizationId: string | undefined) => [...queryKeys.type, 'organization', organizationId] as const,
    type: ['SavedView'] as const,
    view: (organizationId: string | undefined, view: string | undefined) => [...queryKeys.type, 'organization', organizationId, 'view', view] as const
};

export function deleteSavedView(request: { route: { organizationId: string | undefined } }) {
    const queryClient = useQueryClient();

    return createMutation<void, ProblemDetails, SavedView>(() => ({
        enabled: () => !!accessToken.current && !!request.route.organizationId,
        mutationFn: async (savedView: SavedView) => {
            const client = useFetchClient();
            await client.delete(`saved-views/${savedView.id}`, {
                expectedStatusCodes: [202]
            });
        },
        onSuccess: (_data: void, savedView: SavedView) => {
            removeSavedViewFromCaches(queryClient, savedView, request.route.organizationId);
        }
    }));
}

export function getSavedViewsByViewQuery(request: { route: { organizationId: string | undefined; view: string | undefined } }) {
    return createQuery<SavedView[], ProblemDetails>(() => ({
        enabled: () => !!accessToken.current && !!request.route.organizationId && !!request.route.view,
        queryFn: async ({ signal }: { signal: AbortSignal }) => {
            const client = useFetchClient();
            const response = await client.getJSON<SavedView[]>(`organizations/${request.route.organizationId}/saved-views/${request.route.view}`, { signal });
            return response.data!;
        },
        queryKey: queryKeys.view(request.route.organizationId, request.route.view)
    }));
}

export function getSavedViewsQuery(request: { route: { organizationId: string | undefined } }) {
    return createQuery<SavedView[], ProblemDetails>(() => ({
        enabled: () => !!accessToken.current && !!request.route.organizationId,
        queryFn: async ({ signal }: { signal: AbortSignal }) => {
            const client = useFetchClient();
            const response = await client.getJSON<SavedView[]>(`organizations/${request.route.organizationId}/saved-views`, {
                signal
            });
            return response.data!;
        },
        queryKey: queryKeys.organization(request.route.organizationId)
    }));
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
            syncSavedViewCaches(queryClient, savedView);
        }
    }));
}

export function removeSavedViewFromCaches(queryClient: QueryClient, savedView: SavedView, organizationId: string | undefined = savedView.organization_id) {
    const evict = (cachedViews: SavedView[] | undefined) => cachedViews?.filter((v) => v.id !== savedView.id);
    queryClient.setQueryData(queryKeys.view(organizationId, savedView.view_type), evict);
    queryClient.setQueryData(queryKeys.organization(organizationId), evict);
}

export function syncSavedViewCaches(queryClient: QueryClient, savedView: SavedView, organizationId: string | undefined = savedView.organization_id) {
    queryClient.setQueryData(queryKeys.view(organizationId, savedView.view_type), (cachedViews: SavedView[] | undefined) =>
        upsertSavedViewCache(cachedViews, savedView)
    );
    queryClient.setQueryData(queryKeys.organization(organizationId), (cachedViews: SavedView[] | undefined) => upsertSavedViewCache(cachedViews, savedView));
}

export function upsertSavedViewCache(cachedViews: SavedView[] | undefined, savedView: SavedView): SavedView[] {
    const views = savedView.is_default
        ? (cachedViews ?? []).map((view) => {
              if (view.id === savedView.id || view.view_type !== savedView.view_type || !view.is_default) {
                  return view;
              }

              return { ...view, is_default: false };
          })
        : (cachedViews ?? []);
    const savedViewIndex = views.findIndex((view) => view.id === savedView.id);

    if (savedViewIndex === -1) {
        return [...views, savedView];
    }

    return views.map((view) => (view.id === savedView.id ? savedView : view));
}
