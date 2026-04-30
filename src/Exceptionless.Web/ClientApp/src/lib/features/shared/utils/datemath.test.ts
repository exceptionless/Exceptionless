import { type QuickRangeOption, quickRanges } from '$features/shared/components/date-range-picker/quick-ranges';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';

import {
    extractRangeExpressions,
    getDateMathValidationError,
    isValidDateMath,
    parseDate,
    parseDateMath,
    parseDateMathRange,
    toDateMathRange,
    validateAndResolveTime,
    validateDateMath
} from './datemath';

describe('DateMath Library', () => {
    beforeEach(() => {
        vi.useRealTimers();
        vi.useFakeTimers();
        vi.setSystemTime(new Date('2025-09-20T14:30:00Z'));
    });

    afterEach(() => {
        vi.useRealTimers();
    });

    describe('Core date math parsing', () => {
        it('should parse "now" expression', () => {
            const result = parseDateMath('now');
            expect(result.success).toBe(true);
            expect(result.date).toEqual(new Date('2025-09-20T14:30:00Z'));
            expect(result.expression).toBe('now');
        });

        it('should parse "now-5m" expression', () => {
            const result = parseDateMath('now-5m');
            expect(result.success).toBe(true);
            expect(result.date).toEqual(new Date('2025-09-20T14:25:00Z'));
            expect(result.expression).toBe('now-5m');
        });

        it('should parse "now+1h" expression', () => {
            const result = parseDateMath('now+1h');
            expect(result.success).toBe(true);
            expect(result.date).toEqual(new Date('2025-09-20T15:30:00Z'));
            expect(result.expression).toBe('now+1h');
        });

        it('should parse "now/d" rounding expression', () => {
            const result = parseDateMath('now/d');
            expect(result.success).toBe(true);
            expect(result.date).toEqual(new Date('2025-09-20T00:00:00Z'));
            expect(result.expression).toBe('now/d');
        });

        it('should parse "now-1d/d" complex expression', () => {
            const result = parseDateMath('now-1d/d');
            expect(result.success).toBe(true);
            expect(result.date).toEqual(new Date('2025-09-19T00:00:00Z'));
            expect(result.expression).toBe('now-1d/d');
        });

        it('should handle upper limit rounding', () => {
            const result = parseDateMath('now/d', undefined, true);
            expect(result.success).toBe(true);
            expect(result.date).toEqual(new Date('2025-09-20T23:59:59.999Z'));
        });

        it('should parse explicit date with || operations', () => {
            const result = parseDateMath('2025-09-20||+1d');
            expect(result.success).toBe(true);
            expect(result.date).toEqual(new Date('2025-09-21T00:00:00Z'));
        });

        it('should handle invalid expressions', () => {
            const result = parseDateMath('invalid');
            expect(result.success).toBe(false);
            expect(result.error).toBeDefined();
        });

        it('should handle empty expressions', () => {
            const result = parseDateMath('');
            expect(result.success).toBe(false);
            expect(result.error).toBe('Expression cannot be empty');
        });

        it('should parse wildcard (*) expression', () => {
            const result = parseDateMath('*');
            expect(result.success).toBe(true);
            expect(result.date).toEqual(new Date('1970-01-01T00:00:00.000Z'));
            expect(result.expression).toBe('*');
        });

        it('should parse wildcard (*) expression for upper limit', () => {
            const result = parseDateMath('*', undefined, true);
            expect(result.success).toBe(true);
            expect(result.date).toEqual(new Date('9999-12-31T23:59:59.999Z'));
            expect(result.expression).toBe('*');
        });
    });

    describe('Local datetime formats', () => {
        it('should support local datetime without Z suffix', () => {
            const result = parseDateMath('2025-01-01T00:00:00');
            expect(result.success).toBe(true);
            expect(result.date).toEqual(new Date('2025-01-01T00:00:00'));
        });

        it('should support local datetime with seconds', () => {
            const result = parseDateMath('2025-01-01T10:30:45');
            expect(result.success).toBe(true);
            expect(result.date).toEqual(new Date('2025-01-01T10:30:45'));
        });

        it('should support local datetime with milliseconds', () => {
            const result = parseDateMath('2025-01-01T10:30:45.123');
            expect(result.success).toBe(true);
            expect(result.date).toEqual(new Date('2025-01-01T10:30:45.123'));
        });

        it('should support date without time component', () => {
            const result = parseDateMath('2025-01-01');
            expect(result.success).toBe(false);
            expect(result.error).toContain('Invalid date math expression');
        });

        it('should support UTC datetime with Z suffix', () => {
            const result = parseDateMath('2025-01-01T00:00:00Z');
            expect(result.success).toBe(true);
            expect(result.date).toEqual(new Date('2025-01-01T00:00:00Z'));
        });

        it('should support timezone offsets', () => {
            const result = parseDateMath('2025-01-01T00:00:00-08:00');
            expect(result.success).toBe(true);
            expect(result.date).toEqual(new Date('2025-01-01T00:00:00-08:00'));
        });
    });

    describe('Utility functions', () => {
        it('should validate expressions', () => {
            expect(isValidDateMath('now')).toBe(true);
            expect(isValidDateMath('now-5m')).toBe(true);
            expect(isValidDateMath('*')).toBe(true);
            expect(isValidDateMath('invalid')).toBe(false);
        });

        it('should parse date (throwing version)', () => {
            expect(parseDate('now')).toEqual(new Date('2025-09-20T14:30:00Z'));
            expect(() => parseDate('invalid')).toThrow();
        });

        it('should validate and resolve time', () => {
            expect(validateAndResolveTime('now')).toEqual(new Date('2025-09-20T14:30:00Z'));
            expect(validateAndResolveTime('*')).toEqual(new Date('1970-01-01T00:00:00.000Z'));
            expect(validateAndResolveTime('invalid')).toBeNull();
        });

        it('should get validation errors', () => {
            expect(getDateMathValidationError('now')).toBeNull();
            expect(getDateMathValidationError('*')).toBeNull();
            expect(getDateMathValidationError('invalid')).toBeTruthy();
        });

        it('should validate date math input', () => {
            expect(validateDateMath('now')).toEqual({ valid: true });
            expect(validateDateMath('*')).toEqual({ valid: true });
            expect(validateDateMath('invalid')).toEqual({ error: expect.any(String), valid: false });
        });

        it('should convert dates to range string', () => {
            const start = new Date('2025-09-20T00:00:00Z');
            const end = new Date('2025-09-20T23:59:59Z');
            const result = toDateMathRange(start, end);
            // toLocalISOString converts to local timezone, so we just verify the format
            expect(result).toMatch(/^\[\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}\.\d{3} TO \d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}\.\d{3}\]$/);
        });
    });

    describe('Time units support', () => {
        it('should support all time units', () => {
            const testCases = [
                { expr: 'now+1s', unit: 'seconds' },
                { expr: 'now+1m', unit: 'minutes' },
                { expr: 'now+1h', unit: 'hours' },
                { expr: 'now+1H', unit: 'hours' }, // Backend supports both h and H
                { expr: 'now+1d', unit: 'days' },
                { expr: 'now+1w', unit: 'weeks' },
                { expr: 'now+1M', unit: 'months' },
                { expr: 'now+1y', unit: 'years' }
            ];

            testCases.forEach(({ expr }) => {
                const result = parseDateMath(expr);
                expect(result.success).toBe(true);
            });
        });

        it('should handle rounding for all units', () => {
            const testCases = [
                'now/s',
                'now/m',
                'now/h',
                'now/H', // Backend supports both h and H
                'now/d',
                'now/w',
                'now/M',
                'now/y'
            ];

            testCases.forEach((expr) => {
                const result = parseDateMath(expr);
                expect(result.success).toBe(true);
            });
        });
    });

    describe('Operation validation', () => {
        it('should allow multiple operations in correct order', () => {
            const result = parseDateMath('now+1d-2h');
            expect(result.success).toBe(true);
        });

        it('should require rounding to be last operation', () => {
            const result = parseDateMath('now/d+1h');
            expect(result.success).toBe(false);
            expect(result.error).toContain('Rounding operation must be the final operation');
        });

        it('should not allow multiple rounding operations', () => {
            const result = parseDateMath('now/d/h');
            expect(result.success).toBe(false);
            expect(result.error).toContain('Multiple rounding operations are not allowed');
        });

        it('should validate invalid operations', () => {
            const result = parseDateMath('now+invalid');
            expect(result.success).toBe(false);
            expect(result.error).toContain('Invalid operations');
        });
    });

    describe('Range parsing', () => {
        it('should parse simple range expressions', () => {
            const range = parseDateMathRange('now-1h to now');
            expect(range.start).toEqual(new Date('2025-09-20T13:30:00Z'));
            expect(range.end).toEqual(new Date('2025-09-20T14:30:00Z'));
        });

        it('should parse range expressions with TO keyword', () => {
            const range = parseDateMathRange('now-5m to now+5m');
            expect(range.start).toEqual(new Date('2025-09-20T14:25:00Z'));
            expect(range.end).toEqual(new Date('2025-09-20T14:35:00Z'));
        });

        it('should parse exact date range with TO keyword', () => {
            const range = parseDateMathRange('2025-09-20T00:00:00Z to 2025-09-20T23:59:59Z');
            expect(range.start).toEqual(new Date('2025-09-20T00:00:00Z'));
            expect(range.end).toEqual(new Date('2025-09-20T23:59:59Z'));
        });

        it('should parse mixed range with operators', () => {
            const range = parseDateMathRange('2025-09-20||+1d to now');
            expect(range.start).toEqual(new Date('2025-09-21T00:00:00Z'));
            expect(range.end).toEqual(new Date('2025-09-20T14:30:00Z'));
        });
    });

    describe('Extract range expressions', () => {
        it('should extract simple range', () => {
            const result = extractRangeExpressions('now-1h TO now');
            expect(result).not.toBeNull();
            expect(result!.start).toBe('now-1h');
            expect(result!.end).toBe('now');
        });

        it('should extract range with various separators', () => {
            const result = extractRangeExpressions('now-5m TO now+5m');
            expect(result).not.toBeNull();
            expect(result!.start).toBe('now-5m');
            expect(result!.end).toBe('now+5m');
        });

        it('should handle single expression', () => {
            const result = extractRangeExpressions('now');
            expect(result).toBeNull();
        });
    });

    describe('Quick ranges compatibility', () => {
        it('should handle all quick range options', () => {
            quickRanges.forEach((section) => {
                section.options.forEach((range: QuickRangeOption) => {
                    const result = extractRangeExpressions(range.value);
                    expect(result).not.toBeNull();
                    expect(result!.start).toBeTruthy();
                    expect(result!.end).toBeTruthy();

                    const parsedRange = parseDateMathRange(range.value);
                    expect(parsedRange.start).toBeInstanceOf(Date);
                    expect(parsedRange.end).toBeInstanceOf(Date);
                });
            });
        });
    });

    describe('Timezone handling', () => {
        it('should handle various timezone formats', () => {
            const testCases = ['2025-09-20T10:00:00Z||', '2025-09-20T10:00:00+05:00||', '2025-09-20T10:00:00-08:00||', '2025-09-20||+1d'];

            testCases.forEach((expr) => {
                const result = parseDateMath(expr);
                expect(result.success).toBe(true);
                expect(result.date).toBeInstanceOf(Date);
            });
        });
    });

    describe('Week rounding (Sunday start)', () => {
        it('should round to start of week (Sunday)', () => {
            // 2025-09-20 is a Saturday, so start of week should be Sunday 2025-09-14
            const result = parseDateMath('now/w');
            expect(result.success).toBe(true);
            expect(result.date).toEqual(new Date('2025-09-14T00:00:00Z'));
        });

        it('should round to end of week (Saturday) for upper limit', () => {
            // 2025-09-20 is a Saturday, so end of week should be Saturday 2025-09-20 23:59:59.999
            const result = parseDateMath('now/w', undefined, true);
            expect(result.success).toBe(true);
            expect(result.date).toEqual(new Date('2025-09-20T23:59:59.999Z'));
        });
    });

    describe('|| operations support', () => {
        it('should handle simple || operations', () => {
            const result = parseDateMath('2025-09-20||+1d');
            expect(result.success).toBe(true);
            expect(result.date).toEqual(new Date('2025-09-21T00:00:00Z'));
        });

        it('should handle complex || operations', () => {
            const result = parseDateMath('2025-09-20T10:00:00Z||+1d/d');
            expect(result.success).toBe(true);
            expect(result.date).toEqual(new Date('2025-09-21T00:00:00Z'));
        });

        it('should handle || with upper limit rounding', () => {
            const result = parseDateMath('2025-09-20||+1d/d', undefined, true);
            expect(result.success).toBe(true);
            expect(result.date).toEqual(new Date('2025-09-21T23:59:59.999Z'));
        });
    });

    describe('Range expressions with bracket notation', () => {
        it('should extract range with curly braces and TO keyword', () => {
            const result = extractRangeExpressions('{now-1h to now+1h}');
            expect(result).not.toBeNull();
            expect(result!.start).toBe('now-1h');
            expect(result!.end).toBe('now+1h');
        });

        it('should extract range with square brackets and TO keyword', () => {
            const result = extractRangeExpressions('[2025-01-01||/d to 2025-12-31||/d]');
            expect(result).not.toBeNull();
            expect(result!.start).toBe('2025-01-01||/d');
            expect(result!.end).toBe('2025-12-31||/d');
        });

        it('should parse range with curly braces', () => {
            const range = parseDateMathRange('{now-1h to now}');
            expect(range.start).toEqual(new Date('2025-09-20T13:30:00Z'));
            expect(range.end).toEqual(new Date('2025-09-20T14:30:00Z'));
        });

        it('should parse range with square brackets', () => {
            const range = parseDateMathRange('[now-2h to now-1h]');
            expect(range.start).toEqual(new Date('2025-09-20T12:30:00Z'));
            expect(range.end).toEqual(new Date('2025-09-20T13:30:00Z'));
        });
    });

    describe('Wildcard support (*)', () => {
        it('should handle * as a valid character in range expressions', () => {
            const result = extractRangeExpressions('* to now');
            expect(result).not.toBeNull();
            expect(result!.start).toBe('*');
            expect(result!.end).toBe('now');
        });

        it('should handle * in bracket notation', () => {
            const result = extractRangeExpressions('[* to 2025-12-31||/d]');
            expect(result).not.toBeNull();
            expect(result!.start).toBe('*');
            expect(result!.end).toBe('2025-12-31||/d');
        });

        it('should handle * in curly braces', () => {
            const result = extractRangeExpressions('{* to now/d}');
            expect(result).not.toBeNull();
            expect(result!.start).toBe('*');
            expect(result!.end).toBe('now/d');
        });

        it('should handle ranges ending with *', () => {
            const result = extractRangeExpressions('now-1d to *');
            expect(result).not.toBeNull();
            expect(result!.start).toBe('now-1d');
            expect(result!.end).toBe('*');
        });

        it('should handle * to * range', () => {
            const result = extractRangeExpressions('* to *');
            expect(result).not.toBeNull();
            expect(result!.start).toBe('*');
            expect(result!.end).toBe('*');
        });
    });

    describe('Complex range patterns', () => {
        it('should handle standard Elasticsearch range patterns', () => {
            const patterns = ['now-1h to now', '[now-1h to now]', '{now-1h to now}'];

            patterns.forEach((pattern) => {
                const result = extractRangeExpressions(pattern);
                expect(result).not.toBeNull();
                expect(result!.start).toBe('now-1h');
                expect(result!.end).toBe('now');
            });
        });

        it('should handle complex date math in ranges', () => {
            const result = extractRangeExpressions('[2025-01-01||+1M/M to 2025-12-31||/y]');
            expect(result).not.toBeNull();
            expect(result!.start).toBe('2025-01-01||+1M/M');
            expect(result!.end).toBe('2025-12-31||/y');
        });

        it('should handle ranges with timezone information', () => {
            const result = extractRangeExpressions('2025-09-20T00:00:00-08:00||/d to 2025-09-20T23:59:59-08:00||/d');
            expect(result).not.toBeNull();
            expect(result!.start).toBe('2025-09-20T00:00:00-08:00||/d');
            expect(result!.end).toBe('2025-09-20T23:59:59-08:00||/d');
        });

        it('should parse complex ranges with timezone and operations', () => {
            const range = parseDateMathRange('2025-09-20T00:00:00Z||+1d to 2025-09-20T00:00:00Z||+2d');
            expect(range.start).toEqual(new Date('2025-09-21T00:00:00Z'));
            expect(range.end).toEqual(new Date('2025-09-22T00:00:00Z'));
        });

        it('should handle very complex nested operations in ranges', () => {
            const result = extractRangeExpressions('{2025-01-01||+1M-2d/d to now+1y-6M/M}');
            expect(result).not.toBeNull();
            expect(result!.start).toBe('2025-01-01||+1M-2d/d');
            expect(result!.end).toBe('now+1y-6M/M');
        });
    });

    describe('Edge cases and validation', () => {
        it('should return null for invalid bracket notation', () => {
            const invalidPatterns = [
                '{now-1h', // Missing closing brace
                'now-1h}', // Missing opening brace
                '[now-1h to', // Incomplete range
                '{}', // Empty braces
                '[]', // Empty brackets
                '{   }', // Only spaces in braces
                '[   ]' // Only spaces in brackets
            ];

            invalidPatterns.forEach((pattern) => {
                const result = extractRangeExpressions(pattern);
                expect(result).toBeNull();
            });
        });

        it('should handle ranges with wildcard validation', () => {
            // Test that * is accepted as a valid range component with proper TO separator
            const validWildcardPatterns = ['* to now', 'now to *', '[* to *]', '{* to 2025-12-31}'];

            validWildcardPatterns.forEach((pattern) => {
                const result = extractRangeExpressions(pattern);
                expect(result).not.toBeNull();
            });
        });

        it('should preserve exact formatting in extracted expressions', () => {
            const result = extractRangeExpressions('[  now-1h   to   now+1h  ]');
            expect(result).not.toBeNull();
            expect(result!.start).toBe('now-1h');
            expect(result!.end).toBe('now+1h');
        });

        it('should reject non-compliant patterns (spaces without TO)', () => {
            const invalidPatterns = [
                'now-1h now', // Space without TO
                '[now-1h now]', // Bracket with space but no TO
                '{now-1h now}', // Curly brace with space but no TO
                'now-1h   now+1h' // Multiple spaces without TO
            ];

            invalidPatterns.forEach((pattern) => {
                const result = extractRangeExpressions(pattern);
                expect(result).toBeNull();
            });
        });
    });
});
