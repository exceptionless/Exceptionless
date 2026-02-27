import { accessToken } from '$features/auth/index.svelte';
import { ChangeType, type WebSocketMessageValue } from '$features/websockets/models';
import { type ProblemDetails, useFetchClient } from '@exceptionless/fetchclient';
import { createMutation, createQuery, QueryClient, useQueryClient } from '@tanstack/svelte-query';

import type { NewSavedView, SavedView, UpdateSavedView } from './models';

// When a new saved view is added, Elasticsearch needs ~1s to index it.
// Without a delay, the background refetch triggered by this invalidation returns
// stale data that omits the new view, causing the URL param to be cleared.
export async function invalidateSavedViewQueries(queryClient: QueryClient, message: WebSocketMessageValue<'SavedViewChanged'>) {
    const { change_type, organization_id } = message;

    if (change_type === ChangeType.Added) {
        await new Promise<void>((resolve) => setTimeout(resolve, 1500));
    }

    if (organization_id) {
        await queryClient.invalidateQueries({ queryKey: queryKeys.organization(organization_id) });
    } else {
        await queryClient.invalidateQueries({ queryKey: queryKeys.type });
    }
}

export const queryKeys = {
    id: (id: string | undefined) => [...queryKeys.type, id] as const,
    organization: (organizationId: string | undefined) => [...queryKeys.type, 'organization', organizationId] as const,
    type: ['SavedView'] as const,
    view: (organizationId: string | undefined, view: string | undefined) => [...queryKeys.type, 'organization', organizationId, 'view', view] as const
};

export function deleteSavedView(request: { route: { ids: string[] } }) {
    const queryClient = useQueryClient();

    return createMutation<void, ProblemDetails, void>(() => ({
        enabled: () => !!accessToken.current && !!request.route.ids?.length,
        mutationFn: async () => {
            const client = useFetchClient();
            await client.delete(`saved-views/${request.route.ids.join(',')}`, {
                expectedStatusCodes: [202]
            });
        },
        onSuccess: () => {
            queryClient.invalidateQueries({ queryKey: queryKeys.type });
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
        queryKey: queryKeys.view(request.route.organizationId, request.route.view),
        // Saved views are managed via optimistic updates and WebSocket events.
        // Disabling focus-triggered refetch prevents the race condition where a dialog
        // closing fires a window focus event, causing a refetch that returns stale
        // Elasticsearch data (1s indexing delay) and overwrites optimistic cache updates.
        refetchOnWindowFocus: false
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
            queryClient.invalidateQueries({ queryKey: queryKeys.organization(savedView.organization_id) });
            queryClient.invalidateQueries({ queryKey: queryKeys.id(request.route.id) });
        }
    }));
}

export function postSavedView(request: { route: { organizationId: string | undefined } }) {
    const queryClient = useQueryClient();

    return createMutation<SavedView, ProblemDetails, NewSavedView & { is_private?: boolean }>(() => ({
        enabled: () => !!accessToken.current && !!request.route.organizationId,
        mutationFn: async (data: NewSavedView & { is_private?: boolean }) => {
            const client = useFetchClient();
            const { is_private, ...body } = data;
            const url = is_private
                ? `organizations/${request.route.organizationId}/saved-views?is_private=true`
                : `organizations/${request.route.organizationId}/saved-views`;
            const response = await client.postJSON<SavedView>(url, body);
            return response.data!;
        },
        onSuccess: (savedView: SavedView) => {
            // Optimistically populate the per-view cache so the new view is immediately
            // available when handleSelect fires, before the background invalidation completes.
            queryClient.setQueryData(queryKeys.view(request.route.organizationId, savedView.view), (old: SavedView[] | undefined) =>
                old ? [...old, savedView] : [savedView]
            );
            queryClient.invalidateQueries({ queryKey: queryKeys.organization(request.route.organizationId) });
        }
    }));
}
