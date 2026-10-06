import { describe, expect, it } from 'vitest';

import { getHttpsRedirectUrl } from './https-redirect';

describe('configured HTTPS navigation', () => {
    it.each([undefined, '', 'false'])('leaves HTTP available when EnableSsl is %s', (setting) => {
        expect(getHttpsRedirectUrl(new URL('http://localhost/login'), setting)).toBeUndefined();
    });

    it.each([
        ['http://localhost/login?redirect=%2Fstack%2Fall#notice', 'https://localhost/login?redirect=%2Fstack%2Fall#notice'],
        ['http://localhost:8080/#!/event/by-ref/order.123?project=abc', 'https://localhost:8080/#!/event/by-ref/order.123?project=abc']
    ])('upgrades %s without changing its destination', (source, expected) => {
        const url = new URL(source);
        expect(getHttpsRedirectUrl(url, 'true')?.href).toBe(expected);
        expect(url.href).toBe(source);
    });

    it('does not redirect an HTTPS page again', () => {
        expect(getHttpsRedirectUrl(new URL('https://localhost/login'), 'true')).toBeUndefined();
    });
});
