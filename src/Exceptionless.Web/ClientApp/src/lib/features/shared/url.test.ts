import { describe, expect, it } from 'vitest';

import { buildUrl, getSafeRedirectUrl } from './url';

describe('buildUrl', () => {
    it('should return null when host is not provided', () => {
        expect(buildUrl()).toBeNull();
    });

    it('should build basic http url', () => {
        expect(buildUrl(false, 'localhost.com')).toBe('http://localhost.com/');
    });

    it('should build basic https url', () => {
        expect(buildUrl(true, 'localhost.com')).toBe('https://localhost.com/');
    });

    it('should include custom port', () => {
        expect(buildUrl(false, 'localhost.com', 8080)).toBe('http://localhost.com:8080/');
    });

    it('should not include standard ports', () => {
        expect(buildUrl(false, 'localhost.com', 80)).toBe('http://localhost.com/');
        expect(buildUrl(true, 'localhost.com', 443)).toBe('https://localhost.com/');
    });

    it('should handle paths correctly', () => {
        expect(buildUrl(false, 'localhost.com', undefined, 'api/v1')).toBe('http://localhost.com/api/v1');
        expect(buildUrl(false, 'localhost.com', undefined, '/api/v1')).toBe('http://localhost.com/api/v1');
    });

    it('should handle query parameters', () => {
        expect(buildUrl(false, 'localhost.com', undefined, undefined, { key: 'value' })).toBe('http://localhost.com/?key=value');
        expect(buildUrl(false, 'localhost.com', undefined, 'api', { key: 'value', other: 'param' })).toBe('http://localhost.com/api?key=value&other=param');
    });

    it('should handle IP addresses', () => {
        expect(buildUrl(false, '192.168.0.1')).toBe('http://192.168.0.1/');
        expect(buildUrl(true, '192.168.0.1')).toBe('https://192.168.0.1/');
        expect(buildUrl(false, '192.168.0.1', 8080)).toBe('http://192.168.0.1:8080/');
    });
});

describe('getSafeRedirectUrl', () => {
    const fallback = '/default';

    describe('valid redirects', () => {
        it('should allow simple relative paths', () => {
            expect(getSafeRedirectUrl('/dashboard', fallback)).toBe('/dashboard');
            expect(getSafeRedirectUrl('/app/issues', fallback)).toBe('/app/issues');
            expect(getSafeRedirectUrl('/', fallback)).toBe('/');
        });

        it('should allow paths with query parameters', () => {
            expect(getSafeRedirectUrl('/app?filter=test', fallback)).toBe('/app?filter=test');
            expect(getSafeRedirectUrl('/app/issues?page=1&size=10', fallback)).toBe('/app/issues?page=1&size=10');
        });

        it('should allow paths with hash fragments', () => {
            expect(getSafeRedirectUrl('/app#section', fallback)).toBe('/app#section');
            expect(getSafeRedirectUrl('/app/issues#details', fallback)).toBe('/app/issues#details');
        });

        it('should allow paths with encoded characters', () => {
            expect(getSafeRedirectUrl('/app?filter=%20test', fallback)).toBe('/app?filter=%20test');
        });

        it('should trim whitespace from valid paths', () => {
            expect(getSafeRedirectUrl('  /dashboard  ', fallback)).toBe('/dashboard');
        });
    });

    describe('invalid redirects - fallback returned', () => {
        it('should return fallback for null or undefined', () => {
            expect(getSafeRedirectUrl(null, fallback)).toBe(fallback);
            expect(getSafeRedirectUrl(undefined, fallback)).toBe(fallback);
        });

        it('should return fallback for empty string', () => {
            expect(getSafeRedirectUrl('', fallback)).toBe(fallback);
            expect(getSafeRedirectUrl('   ', fallback)).toBe(fallback);
        });

        it('should reject absolute URLs (open redirect attack)', () => {
            // Using example.com per RFC 2606 for documentation/testing
            expect(getSafeRedirectUrl('https://example.com', fallback)).toBe(fallback);
            expect(getSafeRedirectUrl('http://example.com', fallback)).toBe(fallback);
            expect(getSafeRedirectUrl('https://example.com/path', fallback)).toBe(fallback);
        });

        it('should reject protocol-relative URLs', () => {
            expect(getSafeRedirectUrl('//example.com', fallback)).toBe(fallback);
            expect(getSafeRedirectUrl('//example.com/path', fallback)).toBe(fallback);
            expect(getSafeRedirectUrl('//localhost', fallback)).toBe(fallback);
        });

        it('should reject javascript: protocol', () => {
            expect(getSafeRedirectUrl('javascript:alert(1)', fallback)).toBe(fallback);
            expect(getSafeRedirectUrl('JAVASCRIPT:alert(1)', fallback)).toBe(fallback);
        });

        it('should reject data: URLs', () => {
            expect(getSafeRedirectUrl('data:text/html,<script>alert(1)</script>', fallback)).toBe(fallback);
        });

        it('should reject vbscript: protocol', () => {
            expect(getSafeRedirectUrl('vbscript:msgbox(1)', fallback)).toBe(fallback);
        });

        it('should reject paths not starting with /', () => {
            expect(getSafeRedirectUrl('dashboard', fallback)).toBe(fallback);
            expect(getSafeRedirectUrl('app/issues', fallback)).toBe(fallback);
        });

        it('should allow URL-encoded paths (they stay on same origin)', () => {
            // /%2Fexample.com is a valid relative path to /[encoded slash]example.com on current host
            // The browser won't decode it to //example.com - it navigates to the literal path
            expect(getSafeRedirectUrl('/%2Fexample.com', fallback)).toBe('/%2Fexample.com');
        });
    });

    describe('null fallback behavior', () => {
        it('should return null when fallback is null and redirect is invalid', () => {
            expect(getSafeRedirectUrl(null, null)).toBeNull();
            expect(getSafeRedirectUrl(undefined, null)).toBeNull();
            expect(getSafeRedirectUrl('https://example.com', null)).toBeNull();
        });

        it('should return valid redirect even when fallback is null', () => {
            expect(getSafeRedirectUrl('/dashboard', null)).toBe('/dashboard');
            expect(getSafeRedirectUrl('/app/issues', null)).toBe('/app/issues');
        });
    });
});
