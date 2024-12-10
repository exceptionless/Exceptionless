import type { WebSocketMessageValue } from '$features/websockets/models';

import { accessToken } from '$features/auth/index.svelte';
import { type ProblemDetails, useFetchClient } from '@exceptionless/fetchclient';
import { createMutation, createQuery, QueryClient, useQueryClient } from '@tanstack/svelte-query';

import type { Stack, StackStatus } from './models';

//
export async function invalidateStackQueries(queryClient: QueryClient, message: WebSocketMessageValue<'StackChanged'>) {
    const { id } = message;
    if (id) {
        await queryClient.invalidateQueries({ queryKey: queryKeys.id(id) });
    } else {
        await queryClient.invalidateQueries({ queryKey: queryKeys.type });
    }
}

export const queryKeys = {
    id: (id: string | undefined) => [...queryKeys.type, id] as const,
    type: ['Stack'] as const
};

export interface GetStackByIdProps {
    id: string | undefined;
}

export interface UpdateStackFixedStatusProps {
    id: string | undefined;
}

export interface UpdateStackSnoozedStatusProps {
    id: string | undefined;
}

export interface UpdateStackStatusProps {
    id: string | undefined;
}

export function getStackByIdQuery(props: GetStackByIdProps) {
    return createQuery<Stack, ProblemDetails>(() => ({
        enabled: () => !!accessToken.value && !!props.id,
        queryFn: async ({ signal }: { signal: AbortSignal }) => {
            const client = useFetchClient();
            const response = await client.getJSON<Stack>(`stacks/${props.id}`, {
                signal
            });

            return response.data!;
        },
        queryKey: queryKeys.id(props.id)
    }));
}

export function mutateStackFixedStatus(props: UpdateStackFixedStatusProps) {
    const queryClient = useQueryClient();
    return createMutation<void, ProblemDetails, string | undefined>(() => ({
        enabled: () => !!accessToken.value && !!props.id,
        mutationFn: async (version?: string) => {
            const client = useFetchClient();
            await client.post(`stacks/${props.id}/mark-fixed`, undefined, { params: { version } });
        },
        mutationKey: queryKeys.id(props.id),
        onError: () => {
            queryClient.invalidateQueries({ queryKey: queryKeys.id(props.id) });
        },
        onSuccess: () => {
            queryClient.invalidateQueries({ queryKey: queryKeys.id(props.id) });
        }
    }));
}
export function mutateStackSnoozedStatus(props: UpdateStackSnoozedStatusProps) {
    const queryClient = useQueryClient();
    return createMutation<void, ProblemDetails, Date>(() => ({
        enabled: () => !!accessToken.value && !!props.id,
        mutationFn: async (snoozeUntilUtc: Date) => {
            const client = useFetchClient();
            await client.post(`stacks/${props.id}/mark-snoozed`, undefined, { params: { snoozeUntilUtc: snoozeUntilUtc.toISOString() } });
        },
        mutationKey: queryKeys.id(props.id),
        onError: () => {
            queryClient.invalidateQueries({ queryKey: queryKeys.id(props.id) });
        },
        onSuccess: () => {
            queryClient.invalidateQueries({ queryKey: queryKeys.id(props.id) });
        }
    }));
}

export function mutateStackStatus(props: UpdateStackStatusProps) {
    const queryClient = useQueryClient();
    return createMutation<void, ProblemDetails, StackStatus>(() => ({
        enabled: () => !!accessToken.value && !!props.id,
        mutationFn: async (status: StackStatus) => {
            const client = useFetchClient();
            await client.post(`stacks/${props.id}/change-status`, undefined, { params: { status } });
        },
        mutationKey: queryKeys.id(props.id),
        onError: () => {
            queryClient.invalidateQueries({ queryKey: queryKeys.id(props.id) });
        },
        onSuccess: () => {
            queryClient.invalidateQueries({ queryKey: queryKeys.id(props.id) });
        }
    }));
}

export async function prefetchStack(props: GetStackByIdProps) {
    if (!accessToken.value) {
        return;
    }

    const queryClient = useQueryClient();
    await queryClient.prefetchQuery<Stack, ProblemDetails>({
        queryFn: async ({ signal }: { signal: AbortSignal }) => {
            const client = useFetchClient();
            const response = await client.getJSON<Stack>(`stacks/${props.id}`, {
                signal
            });

            return response.data!;
        },
        queryKey: queryKeys.id(props.id)
    });
}
