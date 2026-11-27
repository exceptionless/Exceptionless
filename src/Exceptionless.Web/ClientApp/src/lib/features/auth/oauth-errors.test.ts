import { FetchClient, FetchClientError, ProblemDetails } from '@foundatiofx/fetchclient';
import { describe, expect, it, vi } from 'vitest';

import { getOAuthErrorMessage } from './oauth-errors';

async function rejectedTokenExchange(title: string): Promise<unknown> {
    const fetch = vi
        .fn()
        .mockResolvedValue(new Response(JSON.stringify({ status: 403, title }), { headers: { 'Content-Type': 'application/problem+json' }, status: 403 }));
    const client = new FetchClient({ baseUrl: 'http://localhost/api/v2/', fetch });

    try {
        await client.postJSON('auth/github', {});
    } catch (error) {
        return error;
    }

    throw new Error('Expected the token exchange to fail.');
}

describe('getOAuthErrorMessage', () => {
    it('explains a blocked popup', () => {
        // Arrange
        const error = new Error('Failed to open popup window');

        // Act
        const message = getOAuthErrorMessage(error, 'GitHub');

        // Assert
        expect(message.title).toBe('Your browser blocked the GitHub sign-in window');
    });

    it.each(['The popup window was closed', 'access_denied'])('treats %s as a cancellation', (reason) => {
        // Arrange
        const error = new Error(reason);

        // Act
        const message = getOAuthErrorMessage(error, 'Google');

        // Assert
        expect(message.title).toBe('Google sign-in was cancelled');
    });

    it('shows the server explanation from a rejected token exchange', async () => {
        // Arrange
        const error = await rejectedTokenExchange('Account Creation is currently disabled.');

        // Act
        const message = getOAuthErrorMessage(error, 'Microsoft');

        // Assert
        expect(error).toBeInstanceOf(FetchClientError);
        expect(message).toEqual({
            description: 'Account Creation is currently disabled.',
            title: "We couldn't sign you in with Microsoft"
        });
    });

    it('shows the server explanation when the app throws problem details', () => {
        // Arrange
        const problem = new ProblemDetails();
        problem.title = 'Account is disabled';

        // Act
        const message = getOAuthErrorMessage(problem, 'GitHub');

        // Assert
        expect(message.description).toBe('Account is disabled');
    });
});
