import { describe, expect, it } from 'vitest';

import { isOAuthPopupCallback } from './oauth-callback';

describe('OAuth popup callback', () => {
    it.each(['/?code=123&state=abc', '/?error=access_denied', '/#code=123&state=abc', '/#access_token=123'])(
        'holds the root callback parameters for the opener: %s',
        (path) => expect(isOAuthPopupCallback(new URL(path, 'https://localhost'), true)).toBe(true)
    );

    it.each(['/', '/?filter=test', '/login?code=123', '/oauth/authorize?code=123'])('allows ordinary app navigation: %s', (path) =>
        expect(isOAuthPopupCallback(new URL(path, 'https://localhost'), true)).toBe(false)
    );

    it('does not hold normal top-level navigation', () => {
        expect(isOAuthPopupCallback(new URL('https://localhost/?code=123'), false)).toBe(false);
    });
});
