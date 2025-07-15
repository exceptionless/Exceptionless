import type { WebSocketMessageValue } from '$features/websockets/models';

import { accessToken } from '$features/auth/index.svelte';
import { type FetchClientResponse, ProblemDetails, useFetchClient } from '@exceptionless/fetchclient';
import { createMutation, createQuery, QueryClient, useQueryClient } from '@tanstack/svelte-query';

import { UpdateEmailAddressResult, type UpdateUser, UpdateUserEmailAddress, User, ViewUser } from './models';

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
    ids: (ids: string[] | undefined) => [...queryKeys.type, ...(ids ?? [])] as const,
    me: () => [...queryKeys.type, 'me'] as const,
    organization: (id: string | undefined) => [...queryKeys.type, 'organization', id] as const,
    patchUser: (id: string | undefined) => [...queryKeys.id(id), 'patch'] as const,
    postEmailAddress: (id: string | undefined) => [...queryKeys.idEmailAddress(id), 'update'] as const,
    type: ['User'] as const
};

export interface GetOrganizationUsersParams {
    limit?: number;
    page?: number;
}

export interface GetOrganizationUsersRequest {
    params?: GetOrganizationUsersParams;
    route: {
        organizationId: string | undefined;
    };
}

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

export interface ResendVerificationEmailRequest {
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

export function getOrganizationUsersQuery(request: GetOrganizationUsersRequest) {
    const queryClient = useQueryClient();

    return createQuery<FetchClientResponse<ViewUser[]>, ProblemDetails>(() => ({
        enabled: () => !!accessToken.current && !!request.route.organizationId,
        onSuccess: (data: FetchClientResponse<ViewUser[]>) => {
            data.data?.forEach((user) => {
                queryClient.setQueryData(queryKeys.id(user.id!), user);
            });
        },
        queryClient,
        queryFn: async ({ signal }: { signal: AbortSignal }) => {
            const client = useFetchClient();
            const response = await client.getJSON<ViewUser[]>(`organizations/${request.route.organizationId}/users`, {
                params: {
                    ...request.params,
                    limit: request.params?.limit ?? 1000
                },
                signal
            });

            return response;
        },
        queryKey: [...queryKeys.organization(request.route.organizationId), { params: request.params }]
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
    return createMutation<UpdateEmailAddressResult, ProblemDetails, UpdateUserEmailAddress>(() => ({
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

export function resendVerificationEmail(request: ResendVerificationEmailRequest) {
    return createMutation<void, ProblemDetails, void>(() => ({
        enabled: () => !!accessToken.current && !!request.route.id,
        mutationFn: async () => {
            const client = useFetchClient();
            await client.getJSON<void>(`users/${request.route.id}/resend-verification-email`);
        },
        mutationKey: [...queryKeys.id(request.route.id), 'resend-verification-email']
    }));
}
