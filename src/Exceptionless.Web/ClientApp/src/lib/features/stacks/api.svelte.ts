import type { WebSocketMessageValue } from '$features/websockets/models';

import { accessToken } from '$features/auth/index.svelte';
import { type ProblemDetails, useFetchClient } from '@exceptionless/fetchclient';
import { createQuery, QueryClient, useQueryClient } from '@tanstack/svelte-query';

import type { Stack } from './models';

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
