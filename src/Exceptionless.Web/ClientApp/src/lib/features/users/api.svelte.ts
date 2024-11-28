import type { WebSocketMessageValue } from '$features/websockets/models';

import { accessToken } from '$features/auth/index.svelte';
import { ProblemDetails, useFetchClient } from '@exceptionless/fetchclient';
import { createMutation, createQuery, QueryClient, useQueryClient } from '@tanstack/svelte-query';

import { UpdateEmailAddressResult, type UpdateUser, User } from './models';

export async function invalidateUserQueries(queryClient: QueryClient, message: WebSocketMessageValue<'UserChanged'>) {
    const { id } = message;
    if (id) {
        await queryClient.invalidateQueries({ queryKey: queryKeys.id(id) });

        const currentUser = queryClient.getQueryData<User>(queryKeys.me());
        if (currentUser?.id === id) {
            queryClient.invalidateQueries({ queryKey: queryKeys.me() });
        }
    } else {
        await queryClient.invalidateQueries({ queryKey: queryKeys.all });
    }
}

export const queryKeys = {
    all: ['User'] as const,
    id: (id: string | undefined) => [...queryKeys.all, id] as const,
    idEmailAddress: (id?: string) => [...queryKeys.id(id), 'email-address'] as const,
    me: () => [...queryKeys.all, 'me'] as const
};

export interface UpdateEmailAddressProps {
    id: string | undefined;
}
export interface UpdateUserProps {
    id: string | undefined;
}

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

            return response.data!;
        },
        queryKey: queryKeys.me()
    }));
}

export function mutateEmailAddress(props: UpdateEmailAddressProps) {
    const queryClient = useQueryClient();
    return createMutation<UpdateEmailAddressResult, ProblemDetails, Pick<User, 'email_address'>>(() => ({
        enabled: () => !!accessToken.value && !!props.id,
        mutationFn: async (data: Pick<User, 'email_address'>) => {
            const client = useFetchClient();
            const response = await client.postJSON<UpdateEmailAddressResult>(`users/${props.id}/email-address/${data.email_address}`);
            return response.data!;
        },
        mutationKey: queryKeys.idEmailAddress(props.id),
        onSuccess: (data, variables) => {
            const partialUserData: Partial<User> = { email_address: variables.email_address, is_email_address_verified: data.is_verified };

            const user = queryClient.getQueryData<User>(queryKeys.id(props.id));
            if (user) {
                queryClient.setQueryData(queryKeys.id(props.id), <User>{ ...user, ...partialUserData });
            }

            const currentUser = queryClient.getQueryData<User>(queryKeys.me());
            if (currentUser?.id === props.id) {
                queryClient.setQueryData(queryKeys.me(), <User>{ ...currentUser, ...partialUserData });
            }
        }
    }));
}

export function mutateUser(props: UpdateUserProps) {
    const queryClient = useQueryClient();
    return createMutation<User, ProblemDetails, UpdateUser>(() => ({
        enabled: () => !!accessToken.value && !!props.id,
        mutationFn: async (data: UpdateUser) => {
            const client = useFetchClient();
            const response = await client.patchJSON<User>(`users/${props.id}`, data);
            return response.data!;
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
