import { describe, expect, it } from 'vitest';

import { getInitials } from './strings';

describe('getInitials', () => {
    it('should return default value when input is undefined', () => {
        expect(getInitials(undefined)).toBe('NA');
    });

    it('should return default value when input is empty string', () => {
        expect(getInitials('')).toBe('NA');
    });

    it('should return initials for single name', () => {
        expect(getInitials('john')).toBe('JO');
    });

    it('should return initials for multiple names', () => {
        expect(getInitials('john doe')).toBe('JD');
    });

    it('should handle leading/trailing spaces', () => {
        expect(getInitials('  john  doe  ')).toBe('JD');
    });

    it('should respect maxLength parameter', () => {
        expect(getInitials('john doe smith', 3)).toBe('JDS');
    });

    it('should handle custom default value', () => {
        expect(getInitials(undefined, 2, 'XX')).toBe('XX');
    });

    it('should handle single character input', () => {
        expect(getInitials('j')).toBe('J');
    });

    it('should handle multiple spaces between words', () => {
        expect(getInitials('john    doe')).toBe('JD');
    });
});
