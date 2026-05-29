import type { ReleaseNotification, SystemNotification } from '$features/websockets/models';

import { type ProblemDetails, useFetchClient } from '@exceptionless/fetchclient';
import { createMutation, createQuery, useQueryClient } from '@tanstack/svelte-query';

export const queryKeys = {
    current: ['notifications', 'system'] as const
};

export function clearSystemNotificationMutation() {
    const queryClient = useQueryClient();
    return createMutation<void, ProblemDetails, { publish?: boolean }>(() => ({
        mutationFn: async (params: { publish?: boolean }) => {
            const client = useFetchClient();
            const publish = params.publish !== false;
            await client.delete(`notifications/system?publish=${publish}`);
        },
        onSuccess: () => {
            queryClient.invalidateQueries({ queryKey: queryKeys.current });
        }
    }));
}

export function forceRefreshClientsMutation() {
    return createMutation<ReleaseNotification, ProblemDetails, undefined | { message?: string }>(() => ({
        mutationFn: async (params?: { message?: string }) => {
            const client = useFetchClient();
            const response = params?.message
                ? await client.postJSON<ReleaseNotification>('notifications/force-refresh', { value: params.message })
                : await client.postJSON<ReleaseNotification>('notifications/force-refresh');

            return response.data!;
        }
    }));
}

export function getCurrentSystemNotificationQuery() {
    return createQuery<null | SystemNotification, ProblemDetails>(() => ({
        queryFn: async ({ signal }: { signal: AbortSignal }) => {
            const client = useFetchClient();
            const response = await client.getJSON<SystemNotification>('notifications/system', { signal });

            return response.data ?? null;
        },
        queryKey: queryKeys.current,
        staleTime: 30_000
    }));
}

export function sendReleaseNotificationMutation() {
    return createMutation<ReleaseNotification, ProblemDetails, { critical?: boolean; message?: string }>(() => ({
        mutationFn: async (params: { critical?: boolean; message?: string }) => {
            const client = useFetchClient();
            const critical = params.critical ?? false;
            const response = await client.postJSON<ReleaseNotification>(`notifications/release?critical=${critical}`, {
                value: params.message ?? null
            });

            return response.data!;
        }
    }));
}

export function setSystemNotificationMutation() {
    const queryClient = useQueryClient();
    return createMutation<SystemNotification, ProblemDetails, { message: string; publish?: boolean }>(() => ({
        mutationFn: async (params: { message: string; publish?: boolean }) => {
            const client = useFetchClient();
            const publish = params.publish !== false;
            const response = await client.postJSON<SystemNotification>(`notifications/system?publish=${publish}`, {
                value: params.message
            });

            return response.data!;
        },
        onSuccess: () => {
            queryClient.invalidateQueries({ queryKey: queryKeys.current });
        }
    }));
}
