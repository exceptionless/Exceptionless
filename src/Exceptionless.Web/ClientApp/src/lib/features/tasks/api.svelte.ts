import type { QueryClient } from '@tanstack/svelte-query';

import { type ProblemDetails, useFetchClient } from '@foundatiofx/fetchclient';
import { createMutation, createQuery, useQueryClient } from '@tanstack/svelte-query';

import type { BackgroundTaskUpdated } from '#features/websockets/models.ts';

import { accessToken } from '#features/auth/index.svelte.ts';
import { queryKeys as eventQueryKeys } from '#features/events/api.svelte.ts';
import { queryKeys as stackQueryKeys } from '#features/stacks/api.svelte.ts';

import { applyTaskUpdate, compareTaskUpdateTimes, isTaskActive, type NewBulkOperation, type ViewBackgroundTask } from './models';

/** While a task is active the list is polled as well, so progress still moves when push messages are unavailable. */
export const ACTIVE_TASK_POLL_INTERVAL_MS = 3000;

export const queryKeys = {
    cancel: (id: string | undefined) => [...queryKeys.type, id, 'cancel'] as const,
    organization: (organizationId: string | undefined) => [...queryKeys.type, 'organization', organizationId] as const,
    post: () => [...queryKeys.type, 'post'] as const,
    type: ['BackgroundTask'] as const
};

export interface GetTasksRequest {
    route: {
        organizationId: string | undefined;
    };
}

export function cancelTask() {
    const queryClient = useQueryClient();

    return createMutation<ViewBackgroundTask, ProblemDetails, string>(() => ({
        enabled: () => !!accessToken.current,
        mutationFn: async (id: string) => {
            const client = useFetchClient();
            const response = await client.postJSON<ViewBackgroundTask>(`tasks/${id}/cancel`);

            return response.data!;
        },
        mutationKey: queryKeys.cancel(undefined),
        onSuccess: (task) => upsertTask(queryClient, task)
    }));
}

export function getTasksQuery(request: GetTasksRequest) {
    return createQuery<ViewBackgroundTask[], ProblemDetails>(() => ({
        enabled: () => !!accessToken.current && !!request.route.organizationId,
        queryFn: async ({ signal }: { signal: AbortSignal }) => {
            const client = useFetchClient();
            const response = await client.getJSON<ViewBackgroundTask[]>(`organizations/${request.route.organizationId}/tasks`, {
                signal
            });

            return response.data ?? [];
        },
        queryKey: queryKeys.organization(request.route.organizationId),
        refetchInterval: (query) => (query.state.data?.some((task) => isTaskActive(task.status)) ? ACTIVE_TASK_POLL_INTERVAL_MS : false)
    }));
}

/** Refreshes everything a finished bulk task may have changed. */
export async function invalidateTaskAffectedQueries(queryClient: QueryClient) {
    await Promise.all([
        queryClient.invalidateQueries({
            queryKey: eventQueryKeys.type
        }),
        queryClient.invalidateQueries({
            queryKey: stackQueryKeys.type
        })
    ]);
}

/** Refetches the task lists, e.g. after a request that starts a task. */
export function invalidateTaskQueries(queryClient: QueryClient) {
    return queryClient.invalidateQueries({
        queryKey: queryKeys.type
    });
}

export function postBulkOperation() {
    const queryClient = useQueryClient();

    return createMutation<ViewBackgroundTask, ProblemDetails, NewBulkOperation>(() => ({
        enabled: () => !!accessToken.current,
        mutationFn: async (task: NewBulkOperation) => {
            const client = useFetchClient();
            const response = await client.postJSON<ViewBackgroundTask>('bulk-operations', task);

            return response.data!;
        },
        mutationKey: queryKeys.post(),
        onSuccess: (task) => upsertTask(queryClient, task)
    }));
}

/** Applies a pushed update to the cached list. Unknown tasks (started elsewhere) trigger a refetch instead. */
export async function updateTaskFromMessage(queryClient: QueryClient, update: BackgroundTaskUpdated) {
    const key = queryKeys.organization(update.organization_id);
    const current = queryClient.getQueryData<ViewBackgroundTask[]>(key);
    const next = current ? applyTaskUpdate(current, update) : undefined;
    if (!next) {
        void queryClient.invalidateQueries({
            queryKey: key
        });
        return;
    }

    if (next === current) {
        return;
    }

    await queryClient.cancelQueries(
        {
            queryKey: key
        },
        {
            revert: false
        }
    );
    // Other messages can be applied while cancellation settles; merge with the latest cache, not the earlier snapshot.
    const latest = queryClient.getQueryData<ViewBackgroundTask[]>(key);
    const updated = latest ? applyTaskUpdate(latest, update) : undefined;
    if (updated) {
        queryClient.setQueryData(key, updated);
    }
}

async function upsertTask(queryClient: QueryClient, task: ViewBackgroundTask) {
    const key = queryKeys.organization(task.organization_id);
    // An older list response must not remove newly queued work and stop polling. Keep any pushed updates received
    // since that read started instead of reverting the cache to its pre-fetch snapshot.
    await queryClient.cancelQueries(
        {
            queryKey: key
        },
        {
            revert: false
        }
    );
    queryClient.setQueryData<ViewBackgroundTask[]>(key, (current) => {
        const existing = current?.find((item) => item.id === task.id);
        if (existing && compareTaskUpdateTimes(existing.updated_utc, task.updated_utc) > 0) {
            return current;
        }

        return [task, ...(current ?? []).filter((item) => item.id !== task.id)];
    });
}
