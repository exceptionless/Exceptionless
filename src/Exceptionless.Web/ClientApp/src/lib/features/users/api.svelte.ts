import { accessToken } from '$features/auth/index.svelte';
import { type FetchClientResponse, type ProblemDetails, useFetchClient } from '@exceptionless/fetchclient';
import { createMutation, createQuery, useQueryClient } from '@tanstack/svelte-query';

import type { UpdateUser, User } from './models';

export const queryKeys = {
    all: ['User'] as const,
    id: (id: string | undefined) => [...queryKeys.all, id] as const,
    me: () => [...queryKeys.all, 'me'] as const
};

export function getMeQuery() {
    const queryClient = useQueryClient();

    return createQuery<User, ProblemDetails>(() => ({
        enabled: !!accessToken.value,
        queryClient,
        queryFn: async ({ signal }: { signal: AbortSignal }) => {
            const client = useFetchClient();
            const response = await client.getJSON<User>('users/me', {
                signal
            });

            if (response.ok) {
                queryClient.setQueryData(queryKeys.id(response.data!.id!), response.data);
                return response.data!;
            }

            throw response.problem;
        },
        queryKey: queryKeys.me()
    }));
}

export interface UpdateUserProps {
    id: string | undefined;
}

export function mutateUser(props: UpdateUserProps) {
    const queryClient = useQueryClient();
    return createMutation<FetchClientResponse<unknown>, ProblemDetails, UpdateUser>(() => ({
        enabled: props.id && !!accessToken.value,
        mutationFn: async (data: UpdateUser) => {
            const client = useFetchClient();

            const response = await client.patchJSON<User>(`users/${props.id}`, data);
            if (response.ok) {
                queryClient.setQueryData(queryKeys.id(props.id), response.data);
                return response;
            }

            throw response.problem;
        },
        mutationKey: queryKeys.id(props.id),
        onSettled: () => {
            queryClient.invalidateQueries({ queryKey: queryKeys.id(props.id) });
        }
    }));
}
