import type { WebSocketMessageValue } from '$features/websockets/models';
import type { UserOrganizationPreference } from '$generated/api';
import type { WorkInProgressResult } from '$shared/models';

import { setUserIdentity } from '$features/auth/exceptionless-session';
import { accessToken } from '$features/auth/index.svelte';
import { fetchApiJson } from '$features/shared/api/api.svelte';
import { type FetchClientResponse, ProblemDetails, useFetchClient } from '@foundatiofx/fetchclient';
import { createMutation, createQuery, QueryClient, useQueryClient } from '@tanstack/svelte-query';

import type { OAuthGrant, UpdateEmailAddressResult, UpdateUser, UpdateUserEmailAddress, ViewCurrentUser, ViewUser } from './models';

export async function invalidateUserQueries(queryClient: QueryClient, message: WebSocketMessageValue<'UserChanged'>) {
    const { id } = message;
    if (id) {
        await queryClient.invalidateQueries({
            queryKey: queryKeys.id(id)
        });

        const currentUser = queryClient.getQueryData<ViewCurrentUser>(queryKeys.me());
        if (currentUser?.id === id) {
            queryClient.invalidateQueries({
                queryKey: queryKeys.me()
            });
        }
    } else {
        await queryClient.invalidateQueries({
            queryKey: queryKeys.type
        });
    }
}

export const queryKeys = {
    avatar: (id: string | undefined) => [...queryKeys.id(id), 'avatar'] as const,
    deleteCurrentUser: () => [...queryKeys.me(), 'delete'] as const,
    deleteOAuthGrant: (id: string | undefined) => [...queryKeys.oauthGrants(), id, 'delete'] as const,
    id: (id: string | undefined) => [...queryKeys.type, id] as const,
    idEmailAddress: (id?: string) => [...queryKeys.id(id), 'email-address'] as const,
    ids: (ids: string[] | undefined) => [...queryKeys.type, ...(ids ?? [])] as const,
    me: () => [...queryKeys.type, 'me'] as const,
    oauthGrants: () => [...queryKeys.me(), 'oauth-grants'] as const,
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

export interface UserAvatarRequest {
    route: {
        id: string | undefined;
    };
}

export function deleteCurrentUser() {
    return createMutation<WorkInProgressResult, ProblemDetails, void>(() => ({
        enabled: () => !!accessToken.current,
        mutationFn: async () => {
            const client = useFetchClient();
            const response = await client.deleteJSON<WorkInProgressResult>('users/me');
            return response.data!;
        },
        mutationKey: queryKeys.deleteCurrentUser()
    }));
}

export function deleteOAuthGrantMutation() {
    const queryClient = useQueryClient();
    return createMutation<void, ProblemDetails, string>(() => ({
        enabled: () => !!accessToken.current,
        mutationFn: async (id: string) => {
            const client = useFetchClient();
            await client.delete(`users/me/oauth-grants/${id}`);
        },
        mutationKey: queryKeys.deleteOAuthGrant(undefined),
        onSuccess: () => {
            queryClient.invalidateQueries({
                queryKey: queryKeys.oauthGrants()
            });
        }
    }));
}

export function deleteUserAvatar(request: UserAvatarRequest) {
    const queryClient = useQueryClient();
    return createMutation<ViewCurrentUser, ProblemDetails, void>(() => ({
        enabled: () => !!accessToken.current && !!request.route.id,
        mutationFn: async () => {
            return await fetchApiJson<ViewCurrentUser>(`users/${request.route.id}/avatar`, {
                method: 'DELETE'
            });
        },
        mutationKey: queryKeys.avatar(request.route.id),
        onSuccess: (data) => {
            queryClient.setQueryData(queryKeys.id(request.route.id), data);

            const currentUser = queryClient.getQueryData<ViewCurrentUser>(queryKeys.me());
            if (currentUser?.id === request.route.id) {
                queryClient.setQueryData(queryKeys.me(), data);
            }
        }
    }));
}

export function getMeQuery() {
    const queryClient = useQueryClient();

    return createQuery<ViewCurrentUser, ProblemDetails>(() => ({
        enabled: () => !!accessToken.current,
        onSuccess: async (data: ViewCurrentUser) => {
            queryClient.setQueryData(queryKeys.id(data.id!), data);
            await setUserIdentity(data.id, data.full_name);
        },
        queryClient,
        queryFn: async ({ signal }: { signal: AbortSignal }) => {
            const client = useFetchClient();
            const response = await client.getJSON<ViewCurrentUser>('users/me', {
                signal
            });

            return response.data!;
        },
        queryKey: queryKeys.me()
    }));
}

export function getOAuthGrantsQuery() {
    return createQuery<OAuthGrant[], ProblemDetails>(() => ({
        enabled: () => !!accessToken.current,
        queryFn: async ({ signal }: { signal: AbortSignal }) => {
            const client = useFetchClient();
            const response = await client.getJSON<OAuthGrant[]>('users/me/oauth-grants', {
                signal
            });

            return response.data ?? [];
        },
        queryKey: queryKeys.oauthGrants()
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
        queryKey: [
            ...queryKeys.organization(request.route.organizationId),
            {
                params: request.params
            }
        ]
    }));
}

export function patchUser(request: PatchUserRequest) {
    const queryClient = useQueryClient();
    return createMutation<ViewCurrentUser, ProblemDetails, UpdateUser>(() => ({
        enabled: () => !!accessToken.current && !!request.route.id,
        mutationFn: async (data: UpdateUser) => {
            const client = useFetchClient();
            const response = await client.patchJSON<ViewCurrentUser>(`users/${request.route.id}`, data);
            return response.data!;
        },
        mutationKey: queryKeys.patchUser(request.route.id),
        onError: () => {
            queryClient.invalidateQueries({
                queryKey: queryKeys.id(request.route.id)
            });
        },
        onSuccess: (data) => {
            queryClient.setQueryData(queryKeys.id(request.route.id), data);

            const currentUser = queryClient.getQueryData<ViewCurrentUser>(queryKeys.me());
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
        mutationFn: async (data: Pick<ViewCurrentUser, 'email_address'>) => {
            const client = useFetchClient();
            const response = await client.postJSON<UpdateEmailAddressResult>(`users/${request.route.id}/email-address/${data.email_address}`);
            return response.data!;
        },
        mutationKey: queryKeys.postEmailAddress(request.route.id),
        onSuccess: (data, variables) => {
            const partialUserData: Partial<ViewCurrentUser> = {
                email_address: variables.email_address,
                is_email_address_verified: data.is_verified
            };

            const user = queryClient.getQueryData<ViewCurrentUser>(queryKeys.id(request.route.id));
            if (user) {
                queryClient.setQueryData(queryKeys.id(request.route.id), <ViewCurrentUser>{
                    ...user,
                    ...partialUserData
                });
            }

            const currentUser = queryClient.getQueryData<ViewCurrentUser>(queryKeys.me());
            if (currentUser?.id === request.route.id) {
                queryClient.setQueryData(queryKeys.me(), <ViewCurrentUser>{
                    ...currentUser,
                    ...partialUserData
                });
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

export function setCurrentUserSavedViewDefault(queryClient: QueryClient, organizationId: string, savedViewId: null | string) {
    const currentUser = queryClient.getQueryData<ViewCurrentUser>(queryKeys.me());
    if (!currentUser) {
        return;
    }

    const preference = mergeOrganizationPreferences(currentUser.organization_preferences, organizationId);
    preference.default_saved_view_id = savedViewId;
    setCurrentUserOrganizationPreference(queryClient, currentUser, preference);
}

export function setCurrentUserSavedViewOrder(queryClient: QueryClient, organizationId: string, viewType: string, savedViewIds: string[]): void {
    const currentUser = queryClient.getQueryData<ViewCurrentUser>(queryKeys.me());
    if (!currentUser) {
        return;
    }

    const preference = mergeOrganizationPreferences(currentUser.organization_preferences, organizationId);
    if (savedViewIds.length > 0) {
        preference.saved_view_order[viewType] = [...savedViewIds];
    } else {
        delete preference.saved_view_order[viewType];
    }

    setCurrentUserOrganizationPreference(queryClient, currentUser, preference);
}

export function uploadUserAvatar(request: UserAvatarRequest) {
    const queryClient = useQueryClient();
    return createMutation<ViewCurrentUser, ProblemDetails, File>(() => ({
        enabled: () => !!accessToken.current && !!request.route.id,
        mutationFn: async (file: File) => {
            const data = new FormData();
            data.append('file', file);
            return await fetchApiJson<ViewCurrentUser>(`users/${request.route.id}/avatar`, {
                body: data,
                method: 'POST'
            });
        },
        mutationKey: queryKeys.avatar(request.route.id),
        onSuccess: (data) => {
            queryClient.setQueryData(queryKeys.id(request.route.id), data);

            const currentUser = queryClient.getQueryData<ViewCurrentUser>(queryKeys.me());
            if (currentUser?.id === request.route.id) {
                queryClient.setQueryData(queryKeys.me(), data);
            }
        }
    }));
}

function mergeOrganizationPreferences(organizationPreferences: UserOrganizationPreference[], organizationId: string): UserOrganizationPreference {
    const matches = organizationPreferences.filter((preference) => preference.organization_id === organizationId);
    const defaultSavedViewId = matches
        .map((preference) => preference.default_saved_view_id)
        .filter((savedViewId): savedViewId is string => !!savedViewId)
        .sort()[0];
    const savedViewOrder: Record<string, string[]> = {};

    for (const preference of matches) {
        for (const [viewType, savedViewIds] of Object.entries(preference.saved_view_order ?? {})) {
            if (!savedViewOrder[viewType] && savedViewIds.length > 0) {
                savedViewOrder[viewType] = [...savedViewIds];
            }
        }
    }

    return {
        default_saved_view_id: defaultSavedViewId,
        organization_id: organizationId,
        saved_view_order: savedViewOrder
    };
}

function setCurrentUserOrganizationPreference(queryClient: QueryClient, currentUser: ViewCurrentUser, preference: UserOrganizationPreference): void {
    const organizationPreferences = currentUser.organization_preferences.filter(
        (existingPreference) => existingPreference.organization_id !== preference.organization_id
    );
    if (preference.default_saved_view_id || Object.keys(preference.saved_view_order).length > 0) {
        organizationPreferences.push(preference);
    }

    const updatedUser = {
        ...currentUser,
        organization_preferences: organizationPreferences
    };
    queryClient.setQueryData(queryKeys.me(), updatedUser);
    queryClient.setQueryData(queryKeys.id(currentUser.id), updatedUser);
}
