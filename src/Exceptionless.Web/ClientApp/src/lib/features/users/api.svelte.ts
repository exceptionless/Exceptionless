import { accessToken } from '$features/auth/index.svelte';
import { ProblemDetails, useFetchClient } from '@exceptionless/fetchclient';
import { createMutation, createQuery, useQueryClient } from '@tanstack/svelte-query';

import { type UpdateUser, User } from './models';

export const queryKeys = {
    all: ['User'] as const,
    id: (id: string | undefined) => [...queryKeys.all, id] as const,
    me: () => [...queryKeys.all, 'me'] as const
};

export function getMeQuery() {
    const queryClient = useQueryClient();

    return createQuery<User, ProblemDetails>(() => ({
        enabled: () => !!accessToken.value,
        onSuccess: (data: User) => {
            queryClient.setQueryData(queryKeys.id(data.id!), data);
        },
        queryClient,
        queryFn: async ({ signal }: { signal: AbortSignal }) => {
            const client = useFetchClient();
            const response = await client.getJSON<User>('users/me', {
                signal
            });

            if (response.ok) {
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
    return createMutation<User, ProblemDetails, UpdateUser>(() => ({
        enabled: () => !!accessToken.value && !!props.id,
        mutationFn: async (data: UpdateUser) => {
            const client = useFetchClient();
            const response = await client.patchJSON<User>(`users/${props.id}`, data);
            if (response.ok) {
                return response.data!;
            }

            throw response.problem;
        },
        mutationKey: queryKeys.id(props.id),
        onError: () => {
            queryClient.invalidateQueries({ queryKey: queryKeys.id(props.id) });
        },
        onSuccess: (data) => {
            queryClient.setQueryData(queryKeys.id(props.id), data);

            const currentUser = queryClient.getQueryData<User>(queryKeys.me());
            if (currentUser?.id === props.id) {
                queryClient.setQueryData(queryKeys.me(), data);
            }
        }
    }));
}
