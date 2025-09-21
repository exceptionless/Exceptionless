import { type QuickRangeOption, quickRanges } from '$features/shared/components/date-range-picker/quick-ranges';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';

import {
    extractRangeExpressions,
    getDateMathValidationError,
    isDateMathError,
    isDateMathRange,
    isPointInTime,
    parseDateMath,
    parseDateMathRange,
    rangeToElasticsearchQuery,
    toElasticsearchRangeQuery,
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

    describe('Single time expressions (point in time)', () => {
        it('should parse "now" as a point in time', () => {
            const result = parseDateMath('now');
            expect(isDateMathError(result)).toBe(false);

            if (isDateMathRange(result)) {
                expect(result.isPointInTime).toBe(true);
                expect(result.start).toEqual(result.end);
                expect(result.start.type).toBe('relative');
                expect(result.start.expression).toBe('now');
            }
        });

        it('should parse "now-5m" as a point in time', () => {
            const result = parseDateMath('now-5m');
            expect(isDateMathError(result)).toBe(false);

            if (isDateMathRange(result)) {
                expect(result.isPointInTime).toBe(true);
                expect(result.start).toEqual(result.end);
                expect(result.start.type).toBe('relative');
                expect(result.start.expression).toBe('now-5m');
            }
        });

        it('should parse absolute time as a point in time', () => {
            const result = parseDateMath('2025-09-20T14:30:00');
            expect(isDateMathError(result)).toBe(false);

            if (isDateMathRange(result)) {
                expect(result.isPointInTime).toBe(true);
                expect(result.start).toEqual(result.end);
                expect(result.start.type).toBe('absolute');
            }
        });
    });

    describe('Range expressions', () => {
        it('should parse simple range "now-5m TO now"', () => {
            const result = parseDateMath('now-5m TO now');
            expect(isDateMathError(result)).toBe(false);

            if (isDateMathRange(result)) {
                expect(result.isPointInTime).toBe(false);
                expect(result.start.expression).toBe('now-5m');
                expect(result.end.expression).toBe('now');
                expect(result.startBoundary).toBe('inclusive');
                expect(result.endBoundary).toBe('inclusive');
            }
        });

        it('should parse Elasticsearch range "[now-5m TO now]"', () => {
            const result = parseDateMath('[now-5m TO now]');
            expect(isDateMathError(result)).toBe(false);

            if (isDateMathRange(result)) {
                expect(result.isPointInTime).toBe(false);
                expect(result.start.expression).toBe('now-5m');
                expect(result.end.expression).toBe('now');
                expect(result.startBoundary).toBe('inclusive');
                expect(result.endBoundary).toBe('inclusive');
            }
        });

        it('should parse exclusive boundaries "{now-5m TO now}"', () => {
            const result = parseDateMath('{now-5m TO now}');
            expect(isDateMathError(result)).toBe(false);

            if (isDateMathRange(result)) {
                expect(result.startBoundary).toBe('exclusive');
                expect(result.endBoundary).toBe('exclusive');
            }
        });
    });

    describe('Validation helpers', () => {
        it('should validate correct expressions', () => {
            expect(validateDateMath('now').valid).toBe(true);
            expect(validateDateMath('now-5m TO now').valid).toBe(true);
            expect(validateDateMath('2025-09-20T14:30:00').valid).toBe(true);
        });

        it('should reject invalid expressions', () => {
            expect(validateDateMath('').valid).toBe(false);
            expect(validateDateMath('invalid').valid).toBe(false);
            expect(getDateMathValidationError('')).toContain('Please enter a time');
        });

        it('should use current date in error messages', () => {
            const today = new Date().toISOString().split('T')[0];
            const error = getDateMathValidationError('');
            expect(error).toContain(today);
            expect(error).toContain(`${today}T14:30:00`);
        });
    });

    describe('Type guards', () => {
        it('should correctly identify point in time expressions', () => {
            const nowResult = parseDateMath('now');
            const rangeResult = parseDateMath('now-5m TO now');

            expect(isPointInTime(nowResult)).toBe(true);
            expect(isPointInTime(rangeResult)).toBe(false);
        });
    });

    describe('extractRangeExpressions', () => {
        it('should return start and end expressions for a simple range', () => {
            const result = extractRangeExpressions('now-5m TO now');
            expect(result).toEqual({ end: 'now', start: 'now-5m' });
        });

        it('should handle bracketed ranges', () => {
            const result = extractRangeExpressions('[2025-01-01T00:00:00 TO 2025-01-02T00:00:00]');
            expect(result).toEqual({
                end: '2025-01-02T00:00:00',
                start: '2025-01-01T00:00:00'
            });
        });

        it('should return null for non-range values', () => {
            expect(extractRangeExpressions('now')).toBeNull();
            expect(extractRangeExpressions('last 24 hours')).toBeNull();
        });

        it('should return null for wildcard ranges', () => {
            expect(extractRangeExpressions('[* TO now]')).toBeNull();
            expect(extractRangeExpressions('now TO *')).toBeNull();
        });
    });

    describe('validateAndResolveTime', () => {
        it('should resolve relative time expressions', () => {
            const result = validateAndResolveTime('now-5m');
            expect(result).toBeInstanceOf(Date);
            expect(result!.getTime()).toBeLessThan(Date.now());
        });

        it('should resolve absolute time expressions', () => {
            const result = validateAndResolveTime('2025-09-20T14:30:00');
            expect(result).toBeInstanceOf(Date);
            expect(result!.getFullYear()).toBe(2025);
            expect(result!.getMonth()).toBe(8); // September (0-indexed)
            expect(result!.getDate()).toBe(20);
            expect(result!.getHours()).toBe(14);
            expect(result!.getMinutes()).toBe(30);
        });

        it('should return null for invalid expressions', () => {
            const result = validateAndResolveTime('invalid');
            expect(result).toBeNull();
        });
    });

    describe('Elasticsearch query generation', () => {
        it('should generate inclusive range queries', () => {
            const range = parseDateMath('[now-5m TO now]');
            expect(isDateMathRange(range)).toBe(true);

            if (isDateMathRange(range)) {
                const query = toElasticsearchRangeQuery(range, 'timestamp');
                expect(query).toEqual({
                    range: {
                        timestamp: {
                            gte: 'now-5m',
                            lte: 'now'
                        }
                    }
                });
            }
        });

        it('should generate exclusive range queries', () => {
            const range = parseDateMath('{now-5m TO now}');
            expect(isDateMathRange(range)).toBe(true);

            if (isDateMathRange(range)) {
                const query = toElasticsearchRangeQuery(range, 'date');
                expect(query).toEqual({
                    range: {
                        date: {
                            gt: 'now-5m',
                            lt: 'now'
                        }
                    }
                });
            }
        });

        it('should include timezone in queries', () => {
            const range = parseDateMath('[now-5m TO now]');
            expect(isDateMathRange(range)).toBe(true);

            if (isDateMathRange(range)) {
                const query = toElasticsearchRangeQuery(range, 'timestamp', 'America/New_York');
                expect(query.range.timestamp?.time_zone).toBe('America/New_York');
            }
        });

        it('should convert range expressions directly to queries', () => {
            const query = rangeToElasticsearchQuery('[now-1h TO now]', 'created_at');
            expect(query).toEqual({
                range: {
                    created_at: {
                        gte: 'now-1h',
                        lte: 'now'
                    }
                }
            });
        });

        it('should return null for invalid range expressions', () => {
            const query = rangeToElasticsearchQuery('invalid', 'field');
            expect(query).toBeNull();
        });
    });

    describe('Default quick ranges', () => {
        it('should have valid quick ranges', () => {
            // Collect all range values from the nested structure
            const allRangeValues: string[] = [];
            quickRanges.forEach((section) => {
                section.options.forEach((item: QuickRangeOption) => {
                    allRangeValues.push(item.value);
                });
            });

            // Test that all default ranges are valid
            allRangeValues.forEach((range) => {
                const result = parseDateMath(range);
                if (isDateMathError(result)) {
                    console.error(`Invalid range: ${range}`, result.error);
                }
                expect(isDateMathError(result)).toBe(false);
                expect(isDateMathRange(result)).toBe(true);
            });
        });

        it('should have proper Elasticsearch range format', () => {
            // Collect all range values from the nested structure
            const allRangeValues: string[] = [];
            quickRanges.forEach((section) => {
                section.options.forEach((item: QuickRangeOption) => {
                    allRangeValues.push(item.value);
                });
            });

            // Test that all ranges use proper [x TO y] format
            allRangeValues.forEach((range) => {
                expect(range).toMatch(/^\[[^\]]+\s+TO\s+[^\]]+\]$/);
            });
        });

        it('should have expected structure', () => {
            expect(Array.isArray(quickRanges)).toBe(true);
            expect(quickRanges.length).toBeGreaterThan(0);

            // Check that each section has required properties
            quickRanges.forEach((section) => {
                expect(typeof section.label).toBe('string');
                expect(Array.isArray(section.options)).toBe(true);
                expect(section.options.length).toBeGreaterThan(0);

                // Check that each item has required properties
                section.options.forEach((item: QuickRangeOption) => {
                    expect(typeof item.label).toBe('string');
                    expect(typeof item.value).toBe('string');
                });
            });
        });
    });
});

// ===== TESTS FOR parseDateMathRange (legacy function) =====

describe('parseDateMathRange', () => {
    const mockNow = new Date('2024-01-15T12:00:00Z');

    beforeEach(() => {
        vi.useRealTimers();
        vi.useFakeTimers();
        vi.setSystemTime(mockNow);
    });

    afterEach(() => {
        vi.useRealTimers();
    });

    const dayInMs = 24 * 60 * 60 * 1000;

    function startOfDay(date: Date) {
        const result = new Date(date);
        result.setHours(0, 0, 0, 0);
        return result;
    }

    function endOfDay(date: Date) {
        const result = startOfDay(date);
        result.setHours(23, 59, 59, 999);
        return result;
    }

    function startOfWeek(date: Date) {
        const result = startOfDay(date);
        const diffToMonday = (result.getDay() + 6) % 7;
        result.setDate(result.getDate() - diffToMonday);
        return result;
    }

    function startOfMonth(date: Date) {
        return new Date(date.getFullYear(), date.getMonth(), 1);
    }

    function endOfMonth(date: Date) {
        const start = startOfMonth(date);
        const nextMonth = new Date(start.getFullYear(), start.getMonth() + 1, 1);
        return new Date(nextMonth.getTime() - 1);
    }

    function startOfQuarter(date: Date) {
        const quarter = Math.floor(date.getMonth() / 3);
        const startMonth = quarter * 3;
        return new Date(date.getFullYear(), startMonth, 1);
    }

    function endOfQuarter(date: Date) {
        const start = startOfQuarter(date);
        const nextQuarter = new Date(start.getFullYear(), start.getMonth() + 3, 1);
        return new Date(nextQuarter.getTime() - 1);
    }

    function startOfYear(date: Date) {
        return new Date(date.getFullYear(), 0, 1);
    }

    function endOfYear(date: Date) {
        const start = startOfYear(date);
        const nextYear = new Date(start.getFullYear() + 1, 0, 1);
        return new Date(nextYear.getTime() - 1);
    }

    it('should return fallback range for empty input', () => {
        const result = parseDateMathRange('all');
        expect(result.start).toEqual(new Date('1900-01-01'));
        expect(result.end).toEqual(mockNow);
    });

    it('should return fallback range for empty string', () => {
        const result = parseDateMathRange('');
        expect(result.start).toEqual(new Date('1900-01-01'));
        expect(result.end).toEqual(mockNow);
    });

    it('should parse "last 24 hours"', () => {
        const result = parseDateMathRange('last 24 hours');
        const expectedStart = new Date(mockNow.getTime() - 24 * 60 * 60 * 1000);

        expect(result.start).toEqual(expectedStart);
        expect(result.end).toEqual(mockNow);
    });

    it('should parse "last hour"', () => {
        const result = parseDateMathRange('last hour');
        const expectedStart = new Date(mockNow.getTime() - 60 * 60 * 1000);

        expect(result.start).toEqual(expectedStart);
        expect(result.end).toEqual(mockNow);
    });

    it('should parse "last week"', () => {
        const result = parseDateMathRange('last week');
        const expectedStart = new Date(mockNow.getTime() - 7 * dayInMs);

        expect(result.start).toEqual(expectedStart);
        expect(result.end).toEqual(mockNow);
    });

    it('should parse "last 30 days"', () => {
        const result = parseDateMathRange('last 30 days');
        const expectedStart = new Date(mockNow.getTime() - 30 * dayInMs);

        expect(result.start).toEqual(expectedStart);
        expect(result.end).toEqual(mockNow);
    });

    it('should parse "last 5 minutes"', () => {
        const result = parseDateMathRange('last 5 minutes');
        const expectedStart = new Date(mockNow.getTime() - 5 * 60 * 1000);

        expect(result.start).toEqual(expectedStart);
        expect(result.end).toEqual(mockNow);
    });

    it('should parse "last 15 minutes"', () => {
        const result = parseDateMathRange('last 15 minutes');
        const expectedStart = new Date(mockNow.getTime() - 15 * 60 * 1000);

        expect(result.start).toEqual(expectedStart);
        expect(result.end).toEqual(mockNow);
    });

    it('should parse "last 30 minutes"', () => {
        const result = parseDateMathRange('last 30 minutes');
        const expectedStart = new Date(mockNow.getTime() - 30 * 60 * 1000);

        expect(result.start).toEqual(expectedStart);
        expect(result.end).toEqual(mockNow);
    });

    it('should parse "last 48 hours"', () => {
        const result = parseDateMathRange('last 48 hours');
        const expectedStart = new Date(mockNow.getTime() - 48 * 60 * 60 * 1000);

        expect(result.start).toEqual(expectedStart);
        expect(result.end).toEqual(mockNow);
    });

    it('should parse "last 90 days"', () => {
        const result = parseDateMathRange('last 90 days');
        const expectedStart = new Date(mockNow.getTime() - 90 * dayInMs);

        expect(result.start).toEqual(expectedStart);
        expect(result.end).toEqual(mockNow);
    });

    it('should parse "today so far"', () => {
        const result = parseDateMathRange('today so far');
        const expectedStart = startOfDay(mockNow);

        expect(result.start).toEqual(expectedStart);
        expect(result.end).toEqual(mockNow);
    });

    it('should parse "this week so far"', () => {
        const result = parseDateMathRange('this week so far');
        const expectedStart = startOfWeek(mockNow);

        expect(result.start).toEqual(expectedStart);
        expect(result.end).toEqual(mockNow);
    });

    it('should parse "this month so far"', () => {
        const result = parseDateMathRange('this month so far');
        const expectedStart = startOfMonth(mockNow);

        expect(result.start).toEqual(expectedStart);
        expect(result.end).toEqual(mockNow);
    });

    it('should parse "this year so far"', () => {
        const result = parseDateMathRange('this year so far');
        const expectedStart = startOfYear(mockNow);

        expect(result.start).toEqual(expectedStart);
        expect(result.end).toEqual(mockNow);
    });

    it('should parse "this quarter so far"', () => {
        const result = parseDateMathRange('this quarter so far');
        const expectedStart = startOfQuarter(mockNow);

        expect(result.start).toEqual(expectedStart);
        expect(result.end).toEqual(mockNow);
    });

    it('should parse "previous day"', () => {
        const result = parseDateMathRange('previous day');
        const start = startOfDay(new Date(startOfDay(mockNow).getTime() - dayInMs));
        const end = endOfDay(start);

        expect(result.start).toEqual(start);
        expect(result.end).toEqual(end);
    });

    it('should parse "previous week"', () => {
        const result = parseDateMathRange('previous week');
        const currentWeekStart = startOfWeek(mockNow);
        const previousWeekEnd = new Date(currentWeekStart.getTime() - 1);
        const start = startOfWeek(previousWeekEnd);

        expect(result.start).toEqual(start);
        expect(result.end).toEqual(previousWeekEnd);
    });

    it('should parse "previous month"', () => {
        const result = parseDateMathRange('previous month');
        const currentMonthStart = startOfMonth(mockNow);
        const previousMonthEnd = new Date(currentMonthStart.getTime() - 1);
        const start = startOfMonth(previousMonthEnd);
        const end = endOfMonth(previousMonthEnd);

        expect(result.start).toEqual(start);
        expect(result.end).toEqual(end);
    });

    it('should parse "previous quarter"', () => {
        const result = parseDateMathRange('previous quarter');
        const currentQuarterStart = startOfQuarter(mockNow);
        const previousQuarterEnd = new Date(currentQuarterStart.getTime() - 1);
        const start = startOfQuarter(previousQuarterEnd);
        const end = endOfQuarter(previousQuarterEnd);

        expect(result.start).toEqual(start);
        expect(result.end).toEqual(end);
    });

    it('should parse "previous year"', () => {
        const result = parseDateMathRange('previous year');
        const start = startOfYear(new Date(mockNow.getFullYear() - 1, 0, 1));
        const end = endOfYear(start);

        expect(result.start).toEqual(start);
        expect(result.end).toEqual(end);
    });

    it('should parse custom date range with TO delimiter', () => {
        const customRange = '2024-01-01T00:00:00 TO 2024-01-31T23:59:59';
        const result = parseDateMathRange(customRange);

        expect(result.start).toEqual(new Date('2024-01-01T00:00:00'));
        expect(result.end).toEqual(new Date('2024-01-31T23:59:59'));
    });

    it('should parse custom range with incomplete seconds', () => {
        const customRange = '2024-01-01T00:00 TO 2024-01-31T23:59';
        const result = parseDateMathRange(customRange);

        expect(result.start).toEqual(new Date('2024-01-01T00:00:00'));
        expect(result.end).toEqual(new Date('2024-01-31T23:59:00'));
    });

    it('should parse date-only range', () => {
        const customRange = '2024-01-01 TO 2024-01-31';
        const result = parseDateMathRange(customRange);

        expect(result.start).toEqual(new Date('2024-01-01'));
        expect(result.end).toEqual(new Date('2024-01-31'));
    });

    it('should return fallback for malformed range', () => {
        const result = parseDateMathRange('invalid-range');
        const expectedStart = new Date(mockNow.getTime() - 7 * dayInMs);

        expect(result.start).toEqual(expectedStart);
        expect(result.end).toEqual(mockNow);
    });

    it('should parse single date as fallback', () => {
        const result = parseDateMathRange('2024-01-01');
        const expectedStart = new Date(mockNow.getTime() - 7 * dayInMs);

        expect(result.start).toEqual(expectedStart);
        expect(result.end).toEqual(mockNow);
    });

    it('should return fallback for unrecognized format', () => {
        const result = parseDateMathRange('unknown');
        const expectedStart = new Date(mockNow.getTime() - 7 * dayInMs);

        expect(result.start).toEqual(expectedStart);
        expect(result.end).toEqual(mockNow);
    });

    it('should parse range with explicit delimiter', () => {
        const customRange = '2024-01-01T00:00:00..2024-01-31T23:59:59';
        const result = parseDateMathRange(customRange);

        expect(result.start).toEqual(new Date('2024-01-01T00:00:00'));
        expect(result.end).toEqual(new Date('2024-01-31T23:59:59'));
    });

    it('should parse date range with timezone information', () => {
        const customRange = '2024-01-01T00:00:00Z..2024-01-31T23:59:59Z';
        const result = parseDateMathRange(customRange);

        expect(result.start).toEqual(new Date('2024-01-01T00:00:00Z'));
        expect(result.end).toEqual(new Date('2024-01-31T23:59:59Z'));
    });

    it('should parse date range with positive timezone offset', () => {
        const customRange = '2024-01-01T00:00:00+05:00..2024-01-31T23:59:59+05:00';
        const result = parseDateMathRange(customRange);

        expect(result.start).toEqual(new Date('2024-01-01T00:00:00+05:00'));
        expect(result.end).toEqual(new Date('2024-01-31T23:59:59+05:00'));
    });

    it('should parse date range with negative timezone offset', () => {
        const customRange = '2024-01-01T00:00:00-08:00..2024-01-31T23:59:59-08:00';
        const result = parseDateMathRange(customRange);

        expect(result.start).toEqual(new Date('2024-01-01T00:00:00-08:00'));
        expect(result.end).toEqual(new Date('2024-01-31T23:59:59-08:00'));
    });

    it('should handle timezone offset without colon', () => {
        const customRange = '2024-01-01T00:00:00-0800..2024-01-31T23:59:59-0800';
        const result = parseDateMathRange(customRange);

        expect(result.start).toEqual(new Date('2024-01-01T00:00:00-08:00'));
        expect(result.end).toEqual(new Date('2024-01-31T23:59:59-08:00'));
    });

    it('should parse mixed timezone formats', () => {
        const customRange = '2024-01-01T00:00:00Z 2024-01-31T23:59:59+05:00';
        const result = parseDateMathRange(customRange);

        expect(result.start).toEqual(new Date('2024-01-01T00:00:00Z'));
        expect(result.end).toEqual(new Date('2024-01-31T23:59:59+05:00'));
    });

    it('should parse yesterday', () => {
        const result = parseDateMathRange('yesterday');
        const start = startOfDay(new Date(mockNow.getTime() - dayInMs));
        const end = endOfDay(start);

        expect(result.start).toEqual(start);
        expect(result.end).toEqual(end);
    });
});
