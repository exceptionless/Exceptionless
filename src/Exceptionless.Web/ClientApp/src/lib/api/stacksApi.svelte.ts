import { createQuery, useQueryClient } from '@tanstack/svelte-query';
import type { Stack } from '$lib/models/api';
import { useFetchClient, type ProblemDetails } from '@exceptionless/fetchclient';
import { accessToken } from '$api/auth.svelte';

export const queryKeys = {
    all: ['Stack'] as const,
    allWithFilters: (filters: string) => [...queryKeys.all, { filters }] as const,
    id: (id: string | undefined) => [...queryKeys.all, id] as const
};

export interface GetStackByIdProps {
    id: string | undefined;
}

export async function prefetchStack(props: GetStackByIdProps) {
    if (!accessToken.value) {
        return;
    }

    const queryClient = useQueryClient();
    await queryClient.prefetchQuery<Stack, ProblemDetails>({
        queryKey: queryKeys.id(props.id),
        queryFn: async ({ signal }: { signal: AbortSignal }) => {
            const client = useFetchClient();
            const response = await client.getJSON<Stack>(`stacks/${props.id}`, {
                signal
            });

            if (response.ok) {
                return response.data!;
            }

            throw response.problem;
        }
    });
}

export function getStackByIdQuery(props: GetStackByIdProps) {
    return createQuery<Stack, ProblemDetails>(() => ({
        enabled: !!accessToken.value && !!props.id,
        queryKey: queryKeys.id(props.id),
        queryFn: async ({ signal }: { signal: AbortSignal }) => {
            const client = useFetchClient();
            const response = await client.getJSON<Stack>(`stacks/${props.id}`, {
                signal
            });

            if (response.ok) {
                return response.data!;
            }

            throw response.problem;
        }
    }));
}
