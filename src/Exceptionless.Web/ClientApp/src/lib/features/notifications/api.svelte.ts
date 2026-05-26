import type { ReleaseNotification, SystemNotification } from '$features/websockets/models';

import { type ProblemDetails, useFetchClient } from '@exceptionless/fetchclient';
import { createMutation, createQuery, useQueryClient } from '@tanstack/svelte-query';

import type { ForceRefreshRequest, NotificationSettings, SendReleaseNotificationRequest, SetSystemNotificationRequest } from './models';

export const queryKeys = {
    current: ['notifications', 'system'] as const,
    settings: ['admin', 'notifications'] as const
};

export function clearSystemNotificationMutation() {
    const queryClient = useQueryClient();
    return createMutation<void, ProblemDetails, { publish?: boolean }>(() => ({
        mutationFn: async (params: { publish?: boolean }) => {
            const client = useFetchClient();
            await client.delete(`admin/notifications/system?publish=${params.publish !== false}`);
        },
        onSuccess: () => {
            queryClient.invalidateQueries({ queryKey: queryKeys.settings });
            queryClient.invalidateQueries({ queryKey: queryKeys.current });
        }
    }));
}

export function forceRefreshClientsMutation() {
    return createMutation<ReleaseNotification, ProblemDetails, ForceRefreshRequest | undefined>(() => ({
        mutationFn: async (params?: ForceRefreshRequest) => {
            const client = useFetchClient();
            const response = await client.postJSON<ReleaseNotification>('admin/notifications/force-refresh', params ?? {});
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

export function getNotificationSettingsQuery() {
    return createQuery<NotificationSettings, ProblemDetails>(() => ({
        queryFn: async ({ signal }: { signal: AbortSignal }) => {
            const client = useFetchClient();
            const response = await client.getJSON<NotificationSettings>('admin/notifications', { signal });
            return response.data!;
        },
        queryKey: queryKeys.settings,
        staleTime: 30_000
    }));
}

export function sendReleaseNotificationMutation() {
    return createMutation<ReleaseNotification, ProblemDetails, SendReleaseNotificationRequest>(() => ({
        mutationFn: async (params: SendReleaseNotificationRequest) => {
            const client = useFetchClient();
            const response = await client.postJSON<ReleaseNotification>('admin/notifications/release', params);
            return response.data!;
        }
    }));
}

export function setSystemNotificationMutation() {
    const queryClient = useQueryClient();
    return createMutation<SystemNotification, ProblemDetails, SetSystemNotificationRequest>(() => ({
        mutationFn: async (params: SetSystemNotificationRequest) => {
            const client = useFetchClient();
            const response = await client.putJSON<SystemNotification>('admin/notifications/system', params);
            return response.data!;
        },
        onSuccess: () => {
            queryClient.invalidateQueries({ queryKey: queryKeys.settings });
            queryClient.invalidateQueries({ queryKey: queryKeys.current });
        }
    }));
}
