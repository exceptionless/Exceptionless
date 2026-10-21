import { describe, expect, it } from 'vitest';

import { getOAuthErrorMessage } from './oauth-errors';

describe('getOAuthErrorMessage', () => {
    it('explains a blocked popup', () => {
        expect(getOAuthErrorMessage(new Error('Failed to open popup window'), 'GitHub').title).toBe('Your browser blocked the GitHub sign-in window');
    });

    it.each(['The popup window was closed', 'access_denied'])('treats %s as a cancellation', (message) => {
        expect(getOAuthErrorMessage(new Error(message), 'Google').title).toBe('Google sign-in was cancelled');
    });

    it('uses server problem details when the token exchange fails', () => {
        expect(getOAuthErrorMessage(new Error('x'), 'Microsoft', { title: 'Account is disabled' })).toEqual({
            description: 'Account is disabled',
            title: "We couldn't sign you in with Microsoft"
        });
    });
});
