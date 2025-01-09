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
        await queryClient.invalidateQueries({ queryKey: queryKeys.type });
    }
}

export const queryKeys = {
    id: (id: string | undefined) => [...queryKeys.type, id] as const,
    idEmailAddress: (id?: string) => [...queryKeys.id(id), 'email-address'] as const,
    me: () => [...queryKeys.type, 'me'] as const,
    patchUser: (id: string | undefined) => [...queryKeys.id(id), 'patch'] as const,
    postEmailAddress: (id: string | undefined) => [...queryKeys.idEmailAddress(id), 'update'] as const,
    type: ['User'] as const
};

export interface PatchUserRequest {
    route: {
        id: string | undefined;
    };
}
export interface PostEmailAddressRequest {
    route: {
        id: string | undefined;
    };
}

export function getMeQuery() {
    const queryClient = useQueryClient();

    return createQuery<User, ProblemDetails>(() => ({
        enabled: () => !!accessToken.current,
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

export function patchUser(request: PatchUserRequest) {
    const queryClient = useQueryClient();
    return createMutation<User, ProblemDetails, UpdateUser>(() => ({
        enabled: () => !!accessToken.current && !!request.route.id,
        mutationFn: async (data: UpdateUser) => {
            const client = useFetchClient();
            const response = await client.patchJSON<User>(`users/${request.route.id}`, data);
            return response.data!;
        },
        mutationKey: queryKeys.patchUser(request.route.id),
        onError: () => {
            queryClient.invalidateQueries({ queryKey: queryKeys.id(request.route.id) });
        },
        onSuccess: (data) => {
            queryClient.setQueryData(queryKeys.id(request.route.id), data);

            const currentUser = queryClient.getQueryData<User>(queryKeys.me());
            if (currentUser?.id === request.route.id) {
                queryClient.setQueryData(queryKeys.me(), data);
            }
        }
    }));
}

export function postEmailAddress(request: PostEmailAddressRequest) {
    const queryClient = useQueryClient();
    return createMutation<UpdateEmailAddressResult, ProblemDetails, Pick<User, 'email_address'>>(() => ({
        enabled: () => !!accessToken.current && !!request.route.id,
        mutationFn: async (data: Pick<User, 'email_address'>) => {
            const client = useFetchClient();
            const response = await client.postJSON<UpdateEmailAddressResult>(`users/${request.route.id}/email-address/${data.email_address}`);
            return response.data!;
        },
        mutationKey: queryKeys.postEmailAddress(request.route.id),
        onSuccess: (data, variables) => {
            const partialUserData: Partial<User> = { email_address: variables.email_address, is_email_address_verified: data.is_verified };

            const user = queryClient.getQueryData<User>(queryKeys.id(request.route.id));
            if (user) {
                queryClient.setQueryData(queryKeys.id(request.route.id), <User>{ ...user, ...partialUserData });
            }

            const currentUser = queryClient.getQueryData<User>(queryKeys.me());
            if (currentUser?.id === request.route.id) {
                queryClient.setQueryData(queryKeys.me(), <User>{ ...currentUser, ...partialUserData });
            }
        }
    }));
}
