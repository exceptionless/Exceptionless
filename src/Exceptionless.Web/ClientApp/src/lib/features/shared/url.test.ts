import { describe, expect, it } from 'vitest';

import { buildUrl } from './url';

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
