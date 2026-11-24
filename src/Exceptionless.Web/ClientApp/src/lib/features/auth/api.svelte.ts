import { PUBLIC_INTERCOM_APPID } from '$app/env/public';
import { ProblemDetails, useFetchClient } from '@foundatiofx/fetchclient';
import { createQuery, type QueryClient } from '@tanstack/svelte-query';

import { getIntercomTokenSessionKey, intercomTokenRefreshIntervalMs } from '#features/intercom/config.ts';
import { organization } from '#features/organizations/context.svelte.ts';

import type { Login, TokenResult } from './models';

import { endSession } from './exceptionless-session';
import { clearAuthenticationSession } from './session.svelte';
import { accessToken } from './state.svelte';

const queryKeys = {
    intercom: (accessToken: null | string) => ['Auth', 'intercom', getIntercomTokenSessionKey(accessToken)] as const
};

const INVALID_CREDENTIALS_MESSAGE = 'Invalid email or password';
const TOO_MANY_LOGIN_ATTEMPTS_MESSAGE = 'Too many attempts. Wait a few minutes and try again, or reset your password.';
const TOO_MANY_SIGNUP_ATTEMPTS_MESSAGE = 'Too many sign up attempts. Wait a while and try again.';
const EXISTING_ACCOUNT_MESSAGE = 'An account with this email address already exists. Log in, or reset your password if you forgot it.';

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
        enabled: () => !!accessToken.current && !!PUBLIC_INTERCOM_APPID,
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

export async function login(email: string, password: string, inviteToken?: null | string) {
    const data: Login = {
        email,
        invite_token: inviteToken,
        password
    };
    const client = useFetchClient();
    const response = await client.postJSON<TokenResult>('auth/login', data, {
        expectedStatusCodes: [401, 422]
    });

    if (response.ok && response.data?.token) {
        accessToken.current = response.data.token;
    } else if (response.status === 401) {
        response.problem.setErrorMessage(getUnauthorizedMessage(response.problem, 'login'));
    }

    return response;
}

export async function logout(queryClient?: QueryClient, client = useFetchClient()) {
    await client.get('auth/logout', {
        expectedStatusCodes: [200, 401, 403]
    });
    await endSession();

    await queryClient?.cancelQueries();
    queryClient?.clear();

    clearAuthenticationSession();

    organization.current = undefined;
    if (typeof localStorage !== 'undefined') {
        localStorage.removeItem('organization');
    }
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
        response.problem.setErrorMessage(getUnauthorizedMessage(response.problem, 'signup'));
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

/**
 * Maps an unauthorized login or signup response to a message for the form.
 * The API returns 401 with these titles for rate-limit denials ("Login denied." / "Signup denied.").
 * A signup for an existing account is attempted as a login, so a failed login title means the account exists.
 */
function getUnauthorizedMessage(problem: Pick<ProblemDetails, 'title'>, operation: 'login' | 'signup'): string {
    switch (problem.title) {
        case 'Login denied.':
            return TOO_MANY_LOGIN_ATTEMPTS_MESSAGE;
        case 'Login failed.':
            return operation === 'signup' ? EXISTING_ACCOUNT_MESSAGE : INVALID_CREDENTIALS_MESSAGE;
        case 'Signup denied.':
            return TOO_MANY_SIGNUP_ATTEMPTS_MESSAGE;
        default:
            return INVALID_CREDENTIALS_MESSAGE;
    }
}
