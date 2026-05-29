import { env } from '$env/dynamic/public';
import { getIntercomTokenSessionKey, intercomTokenRefreshIntervalMs } from '$features/intercom/config';
import { organization } from '$features/organizations/context.svelte';
import { ProblemDetails, useFetchClient } from '@exceptionless/fetchclient';
import { hide as hideIntercom, shutdown as shutdownIntercom } from '@intercom/messenger-js-sdk';
import { createQuery, type QueryClient } from '@tanstack/svelte-query';

import type { Login, TokenResult } from './models';

import { accessToken } from './index.svelte';

const queryKeys = {
    intercom: (accessToken: null | string) => ['Auth', 'intercom', getIntercomTokenSessionKey(accessToken)] as const
};

export async function cancelResetPassword(token: string) {
    const client = useFetchClient();
    const response = await client.post(`auth/cancel-reset-password/${token}`, {
        expectedStatusCodes: [400]
    });

    return response;
}

export async function changePassword(currentPassword: string | undefined, newPassword: string) {
    const client = useFetchClient();
    const response = await client.postJSON<TokenResult>(
        'auth/change-password',
        {
            current_password: currentPassword,
            password: newPassword
        },
        {
            expectedStatusCodes: [422]
        }
    );

    if (response.ok && response.data?.token) {
        accessToken.current = response.data.token;
    }

    return response;
}

export async function forgotPassword(email: string) {
    const client = useFetchClient();
    return await client.get(`auth/forgot-password/${email}`);
}

export function getIntercomTokenQuery() {
    return createQuery<TokenResult, ProblemDetails>(() => ({
        enabled: () => !!accessToken.current && !!env.PUBLIC_INTERCOM_APPID,
        queryFn: async ({ signal }) => {
            const client = useFetchClient();
            const response = await client.getJSON<TokenResult>('auth/intercom', {
                signal
            });

            return response.data!;
        },
        queryKey: queryKeys.intercom(accessToken.current),
        refetchInterval: intercomTokenRefreshIntervalMs,
        staleTime: intercomTokenRefreshIntervalMs
    }));
}

/**
 * Checks if an email address is already in use.
 * @param email The email address to check
 * @returns true if the email is already taken (user exists), false if available
 */
export async function isEmailAddressTaken(email: string) {
    const client = useFetchClient();
    const response = await client.get(`auth/check-email-address/${email}`, {
        expectedStatusCodes: [201, 204]
    });

    // Backend returns 201 if email exists (taken), 204 if available
    return response.status === 201;
}

export async function login(email: string, password: string) {
    const data: Login = { email, password };
    const client = useFetchClient();
    const response = await client.postJSON<TokenResult>('auth/login', data, {
        expectedStatusCodes: [401, 422]
    });

    if (response.ok && response.data?.token) {
        accessToken.current = response.data.token;
    } else if (response.status === 401) {
        response.problem.setErrorMessage('Invalid email or password');
    }

    return response;
}

export async function logout(queryClient?: QueryClient, client = useFetchClient()) {
    await client.get('auth/logout', { expectedStatusCodes: [200, 401, 403] });

    await queryClient?.cancelQueries();
    queryClient?.clear();

    if (typeof window !== 'undefined' && 'Intercom' in window && typeof window.Intercom === 'function') {
        hideIntercom();
        shutdownIntercom();
    }

    organization.current = undefined;
    if (typeof localStorage !== 'undefined') {
        localStorage.removeItem('organization');
    }

    accessToken.current = null;
}

export async function resetPassword(passwordResetToken: string, password: string) {
    const client = useFetchClient();
    const response = await client.postJSON<void>(
        'auth/reset-password',
        {
            password,
            password_reset_token: passwordResetToken
        },
        {
            expectedStatusCodes: [422]
        }
    );

    return response;
}

export async function signup(name: string, email: string, password: string, inviteToken?: null | string) {
    const client = useFetchClient();
    const response = await client.postJSON<TokenResult>(
        'auth/signup',
        {
            email,
            invite_token: inviteToken,
            name,
            password
        },
        {
            expectedStatusCodes: [401, 403, 422]
        }
    );

    if (response.ok && response.data?.token) {
        accessToken.current = response.data.token;
    } else if (response.status === 401) {
        response.problem.setErrorMessage('Invalid email or password');
    }

    return response;
}

export async function unlinkOAuthAccount(provider: string, providerUserId: string) {
    const client = useFetchClient();
    const response = await client.postJSON(
        `auth/unlink/${provider}`,
        {
            provider_user_id: providerUserId
        },
        {
            expectedStatusCodes: [400]
        }
    );

    return response;
}
