import { describe, expect, it } from 'vitest';

import { canonicalAppUrl, isSignInCallback } from './legacy-links';

const origin = 'https://localhost:7131';

describe('primary legacy links', () => {
    it.each([
        ['/next/', '/'],
        ['/next/event/123?tab=data#details', '/event/123?tab=data#details'],
        ['/#!/event/by-ref/order.123?project=abc', '/event/by-ref/order.123?project=abc'],
        ['/#/signup?token=a%2Bb%2Fc%3D', '/signup?token=a%2Bb%2Fc%3D'],
        ['/#!/account/verify?token=verify-token', '/account/verify?token=verify-token'],
        ['/next/reset-password/token?cancel=true', '/reset-password/token?cancel=true'],
        ['/account/manage?projectId=abc&tab=notifications', '/account/notifications?project=abc'],
        ['/organization/abc/manage?tab=billing', '/organization/abc/billing'],
        ['/organization/abc/upgrade', '/organization/abc/billing?changePlan=true'],
        ['/organization/abc/frequent', '/organization/abc/dashboard?view=stacks'],
        ['/project/abc/error/timeline', '/project/abc/dashboard?type=error'],
        ['/project/abc/error/frequent?time=all', '/project/abc/dashboard?time=all&type=error&view=stacks'],
        ['/project/abc/error/new', '/project/abc/dashboard?type=error&view=stacks'],
        ['/project/abc/manage?tab=integrations&code=slack-code', '/project/abc/integrations?code=slack-code'],
        ['/stack/abc/mark-fixed', '/stack/abc'],
        ['/stack/abc/ignored', '/stack/abc'],
        ['/stack/abc/discarded', '/stack/abc'],
        ['/next/payment/invoice-id', '/payment/invoice-id'],
        ['/unknown/old/page', '/unknown/old/page'],
        ['/nextdoor', '/nextdoor'],
        ['/event/abc#data', '/event/abc#data']
    ])('maps %s to %s without a second redirect', (input, expected) => {
        const result = canonicalAppUrl(new URL(input, origin));
        expect(result.href).toBe(origin + expected);
        expect(canonicalAppUrl(result).href).toBe(result.href);
    });

    it('retains outer query parameters in hash-router links', () => {
        const result = canonicalAppUrl(new URL('/?source=email#!/signup?token=invite', origin));
        expect(result.searchParams.get('source')).toBe('email');
        expect(result.searchParams.get('token')).toBe('invite');
    });

    it.each(['/#//evil.example/login', '/#!//evil.example/login'])('never follows a protocol-relative hash: %s', (input) => {
        expect(canonicalAppUrl(new URL(input, origin)).origin).toBe(origin);
    });
});

describe('social sign-in callback', () => {
    it.each(['/?code=secret&state=state', '/?error=access_denied', '/#access_token=secret&state=state'])('leaves the response at the origin: %s', (input) => {
        const url = new URL(input, origin);
        expect(isSignInCallback(url)).toBe(true);
        expect(canonicalAppUrl(url).href).toBe(url.href);
    });

    it.each(['/project/abc/integrations?code=slack', '/signup?token=invite', '/#!/event/abc'])('does not intercept %s', (input) => {
        expect(isSignInCallback(new URL(input, origin))).toBe(false);
    });
});
