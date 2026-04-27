import { accessToken } from '$features/auth/index.svelte';
import { ChangeType, type WebSocketMessageValue } from '$features/websockets/models';
import { type ProblemDetails, useFetchClient } from '@exceptionless/fetchclient';
import { createMutation, createQuery, type QueryClient, useQueryClient } from '@tanstack/svelte-query';

import type { NewSavedView, SavedView, UpdateSavedView } from './models';

const ES_REFRESH_DELAY_MS = 1500;

export function invalidateSavedViewQueries(queryClient: QueryClient, message: WebSocketMessageValue<'SavedViewChanged'>) {
    const { organization_id } = message;
    const invalidate = () => {
        if (organization_id) {
            return queryClient.invalidateQueries({ queryKey: queryKeys.organization(organization_id) });
        }

        return queryClient.invalidateQueries({ queryKey: queryKeys.type });
    };

    // Delay invalidation for Added/Saved to avoid overwriting optimistic cache with stale Elasticsearch data.
    if (message.change_type === ChangeType.Added || message.change_type === ChangeType.Saved) {
        return new Promise<void>((resolve) => {
            setTimeout(async () => {
                await invalidate();
                resolve();
            }, ES_REFRESH_DELAY_MS);
        });
    }

    return invalidate();
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
    queryClient.setQueryData(queryKeys.view(organizationId, savedView.view), evict);
    queryClient.setQueryData(queryKeys.organization(organizationId), evict);
}

export function syncSavedViewCaches(queryClient: QueryClient, savedView: SavedView, organizationId: string | undefined = savedView.organization_id) {
    queryClient.setQueryData(queryKeys.view(organizationId, savedView.view), (cachedViews: SavedView[] | undefined) =>
        upsertSavedViewCache(cachedViews, savedView)
    );
    queryClient.setQueryData(queryKeys.organization(organizationId), (cachedViews: SavedView[] | undefined) => upsertSavedViewCache(cachedViews, savedView));
}

export function upsertSavedViewCache(cachedViews: SavedView[] | undefined, savedView: SavedView): SavedView[] {
    const views = savedView.is_default
        ? (cachedViews ?? []).map((view) => {
              if (view.id === savedView.id || view.view !== savedView.view || !view.is_default) {
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
