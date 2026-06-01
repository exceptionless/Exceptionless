import type { SystemNotification } from '$features/websockets/models';

import { type ProblemDetails, useFetchClient } from '@exceptionless/fetchclient';
import { createMutation, createQuery, useQueryClient } from '@tanstack/svelte-query';

export const queryKeys = {
    current: ['notifications', 'system'] as const
};

export function clearSystemNotificationMutation() {
    const queryClient = useQueryClient();
    return createMutation<void, ProblemDetails>(() => ({
        mutationFn: async () => {
            const client = useFetchClient();
            await client.delete('notifications/system');
        },
        onSuccess: () => {
            queryClient.invalidateQueries({ queryKey: queryKeys.current });
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

export function setSystemNotificationMutation() {
    const queryClient = useQueryClient();
    return createMutation<SystemNotification, ProblemDetails, { level?: 'Error' | 'Info' | 'Warning'; message: string; target?: 'Both' | 'Legacy' | 'Modern' }>(
        () => ({
            mutationFn: async (params: { level?: 'Error' | 'Info' | 'Warning'; message: string; target?: 'Both' | 'Legacy' | 'Modern' }) => {
                const client = useFetchClient();
                const response = await client.postJSON<SystemNotification>('notifications/system', {
                    level: params.level ?? 'Info',
                    message: params.message,
                    target: params.target ?? 'Both'
                });

                return response.data!;
            },
            onSuccess: () => {
                queryClient.invalidateQueries({ queryKey: queryKeys.current });
            }
        })
    );
}
