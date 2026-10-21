import { describe, expect, it } from 'vitest';

import { buildAuthPageHref, getAuthPageParams, getPostLoginRedirect, getPostSignupRedirect } from './redirects';

describe('auth page redirects', () => {
    it('keeps safe redirects and invitation tokens', () => {
        expect(getAuthPageParams(new URLSearchParams('redirect=%2Fevent%3Ftime%3D1d&token=abc'))).toEqual({ redirect: '/event?time=1d', token: 'abc' });
    });

    it.each(['https://evil.example.com', '//evil.example.com', 'javascript:alert(1)'])('drops the unsafe redirect %s', (redirect) => {
        expect(getAuthPageParams(new URLSearchParams({ redirect })).redirect).toBeNull();
    });

    it('builds login and signup links that carry the redirect and invitation', () => {
        expect(buildAuthPageHref('/signup', { redirect: '/event?time=1d', token: 'a b' })).toBe('/signup?redirect=%2Fevent%3Ftime%3D1d&token=a+b');
        expect(buildAuthPageHref('/login', { redirect: null, token: 'abc' })).toBe('/login?token=abc');
        expect(buildAuthPageHref('/login', { redirect: null, token: null })).toBe('/login');
    });

    it('sends new accounts to setup and invited accounts to their organization', () => {
        expect(getPostSignupRedirect({ redirect: null, token: null }, 'password')).toBe('/organization/add');
        expect(getPostSignupRedirect({ redirect: null, token: 'abc' }, 'password')).toBe('/');
        expect(getPostSignupRedirect({ redirect: null, token: null }, 'oauth')).toBe('/');
        expect(getPostSignupRedirect({ redirect: '/event', token: 'abc' }, 'password')).toBe('/event');
    });

    it('returns to the requested page after login', () => {
        expect(getPostLoginRedirect({ redirect: '/stack', token: null })).toBe('/stack');
        expect(getPostLoginRedirect({ redirect: null, token: null })).toBe('/');
    });
});
