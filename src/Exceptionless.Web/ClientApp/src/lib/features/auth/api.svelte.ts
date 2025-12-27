import { useFetchClient } from '@exceptionless/fetchclient';

import type { Login, TokenResult } from './models';

import { accessToken } from './index.svelte';

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

export async function logout() {
    const client = useFetchClient();
    await client.get('auth/logout', { expectedStatusCodes: [200, 401] });

    accessToken.current = '';
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
