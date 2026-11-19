import { describe, expect, it } from 'vitest';

import { buildAuthPageHref, getAuthPageParams, getPostLoginRedirect, getPostSignupRedirect } from './redirects';

describe('auth page redirects', () => {
    it('keeps safe redirects and invitation tokens', () => {
        // Arrange
        const query = new URLSearchParams('redirect=%2Fevent%3Ftime%3D1d&token=abc');

        // Act
        const params = getAuthPageParams(query);

        // Assert
        expect(params).toEqual({ redirect: '/event?time=1d', token: 'abc' });
    });

    it.each(['https://evil.example.com', '//evil.example.com', 'javascript:alert(1)'])('drops the unsafe redirect %s', (redirect) => {
        // Arrange
        const query = new URLSearchParams({ redirect });

        // Act
        const params = getAuthPageParams(query);

        // Assert
        expect(params.redirect).toBeNull();
    });

    it.each([
        ['/signup', { redirect: '/event?time=1d', token: 'a b' }, '/signup?redirect=%2Fevent%3Ftime%3D1d&token=a+b'],
        ['/login', { redirect: null, token: 'abc' }, '/login?token=abc'],
        ['/login', { redirect: null, token: null }, '/login']
    ] as const)('builds %s links that carry the redirect and invitation: %j', (path, params, expected) => {
        // Act
        const href = buildAuthPageHref(path, params);

        // Assert
        expect(href).toBe(expected);
    });

    it.each([
        [{ redirect: null, token: null }, 'password', '/organization/add'],
        [{ redirect: null, token: 'abc' }, 'password', '/'],
        [{ redirect: null, token: null }, 'oauth', '/'],
        [{ redirect: '/event', token: 'abc' }, 'password', '/event']
    ] as const)('sends new accounts to setup and invited accounts to their organization: %j via %s', (params, method, expected) => {
        // Act
        const redirect = getPostSignupRedirect(params, method);

        // Assert
        expect(redirect).toBe(expected);
    });

    it.each([
        [{ redirect: '/stack', token: null }, '/stack'],
        [{ redirect: null, token: null }, '/']
    ])('returns to the requested page after login: %j', (params, expected) => {
        // Act
        const redirect = getPostLoginRedirect(params);

        // Assert
        expect(redirect).toBe(expected);
    });
});
