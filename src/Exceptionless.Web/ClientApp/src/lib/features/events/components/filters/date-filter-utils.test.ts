import { beforeEach, describe, expect, it, vi } from 'vitest';

import { parseTimeParameter } from './date-filter-utils';

describe('date-filter-utils', () => {
    beforeEach(() => {
        // Reset mocked timers before each test
        vi.useRealTimers();
    });

    describe('parseTimeParameter', () => {
        const mockNow = new Date('2024-01-15T12:00:00Z');

        beforeEach(() => {
            vi.useFakeTimers();
            vi.setSystemTime(mockNow);
        });

        it('handles "all" time parameter', () => {
            const result = parseTimeParameter('all');
            expect(result.start).toEqual(new Date('1900-01-01'));
            expect(result.end).toEqual(mockNow);
        });

        it('handles empty string time parameter', () => {
            const result = parseTimeParameter('');
            expect(result.start).toEqual(new Date('1900-01-01'));
            expect(result.end).toEqual(mockNow);
        });

        it('handles "last 24 hours" time parameter', () => {
            const result = parseTimeParameter('last 24 hours');
            const expected = new Date(mockNow.getTime() - 24 * 60 * 60 * 1000);
            expect(result.start).toEqual(expected);
            expect(result.end).toEqual(mockNow);
        });

        it('handles "last hour" time parameter', () => {
            const result = parseTimeParameter('last hour');
            const expected = new Date(mockNow.getTime() - 60 * 60 * 1000);
            expect(result.start).toEqual(expected);
            expect(result.end).toEqual(mockNow);
        });

        it('handles "last week" time parameter', () => {
            const result = parseTimeParameter('last week');
            const expected = new Date(mockNow.getTime() - 7 * 24 * 60 * 60 * 1000);

            expect(result.start).toEqual(expected);
            expect(result.end).toEqual(mockNow);
        });

        it('handles "last 30 days" time parameter', () => {
            const result = parseTimeParameter('last 30 days');
            const expected = new Date(mockNow.getTime() - 30 * 24 * 60 * 60 * 1000);

            expect(result.start).toEqual(expected);
            expect(result.end).toEqual(mockNow);
        });

        it('handles custom date range with full timestamps', () => {
            const customRange = '2024-01-01T00:00:00..2024-01-31T23:59:59';
            const result = parseTimeParameter(customRange);

            expect(result.start).toEqual(new Date('2024-01-01T00:00:00'));
            expect(result.end).toEqual(new Date('2024-01-31T23:59:59'));
        });

        it('handles custom date range with date only', () => {
            const customRange = '2024-01-01..2024-01-31';
            const result = parseTimeParameter(customRange);

            expect(result.start).toEqual(new Date('2024-01-01'));
            expect(result.end).toEqual(new Date('2024-01-31'));
        });

        it('handles custom date range with partial times', () => {
            const customRange = '2024-01-01T10:30..2024-01-31T15:45';
            const result = parseTimeParameter(customRange);

            expect(result.start).toEqual(new Date('2024-01-01T10:30'));
            expect(result.end).toEqual(new Date('2024-01-31T15:45'));
        });

        it('handles custom date range with hours only', () => {
            // Hours only format is invalid, should fallback to last week
            const customRange = '2024-01-01T10..2024-01-31T15';
            const result = parseTimeParameter(customRange);

            // Since '2024-01-01T10' is an invalid date format, it should fallback to last week
            const expected = new Date(mockNow.getTime() - 7 * 24 * 60 * 60 * 1000);
            expect(result.start).toEqual(expected);
            expect(result.end).toEqual(mockNow);
        });

        it('falls back to "last week" for invalid custom range', () => {
            const result = parseTimeParameter('invalid-range');
            const expected = new Date(mockNow.getTime() - 7 * 24 * 60 * 60 * 1000);

            expect(result.start).toEqual(expected);
            expect(result.end).toEqual(mockNow);
        });

        it('falls back to "last week" for single date', () => {
            const result = parseTimeParameter('2024-01-01');
            const expected = new Date(mockNow.getTime() - 7 * 24 * 60 * 60 * 1000);

            expect(result.start).toEqual(expected);
            expect(result.end).toEqual(mockNow);
        });

        it('falls back to "last week" for unknown time parameter', () => {
            const result = parseTimeParameter('unknown');
            const expected = new Date(mockNow.getTime() - 7 * 24 * 60 * 60 * 1000);

            expect(result.start).toEqual(expected);
            expect(result.end).toEqual(mockNow);
        });

        it('handles custom date range with date only', () => {
            const customRange = '2024-01-01..2024-01-31';
            const result = parseTimeParameter(customRange);

            expect(result.start).toEqual(new Date('2024-01-01'));
            expect(result.end).toEqual(new Date('2024-01-31'));
        });

        it('handles custom date range with partial times', () => {
            const customRange = '2024-01-01T10:30..2024-01-31T15:45';
            const result = parseTimeParameter(customRange);

            expect(result.start).toEqual(new Date('2024-01-01T10:30'));
            expect(result.end).toEqual(new Date('2024-01-31T15:45'));
        });

        it('handles custom date range with hours only', () => {
            // Hours only format is invalid, should fallback to last week
            const customRange = '2024-01-01T10..2024-01-31T15';
            const result = parseTimeParameter(customRange);
            const expected = new Date(mockNow.getTime() - 7 * 24 * 60 * 60 * 1000);

            expect(result.start).toEqual(expected);
            expect(result.end).toEqual(mockNow);
        });

        it('falls back to "last week" for invalid custom range', () => {
            const result = parseTimeParameter('invalid-range');
            const expected = new Date(mockNow.getTime() - 7 * 24 * 60 * 60 * 1000);

            expect(result.start).toEqual(expected);
            expect(result.end).toEqual(mockNow);
        });

        it('falls back to "last week" for single date', () => {
            const result = parseTimeParameter('2024-01-01');
            const expected = new Date(mockNow.getTime() - 7 * 24 * 60 * 60 * 1000);

            expect(result.start).toEqual(expected);
            expect(result.end).toEqual(mockNow);
        });

        it('falls back to "last week" for unknown time parameter', () => {
            const result = parseTimeParameter('unknown parameter');
            const expected = new Date(mockNow.getTime() - 7 * 24 * 60 * 60 * 1000);

            expect(result.start).toEqual(expected);
            expect(result.end).toEqual(mockNow);
        });

        it('handles custom date range with hyphen separator and full timestamps', () => {
            const customRange = '2025-08-30T14:31:08-2025-08-30T14:31:56';
            const result = parseTimeParameter(customRange);

            expect(result.start).toEqual(new Date('2025-08-30T14:31:08'));
            expect(result.end).toEqual(new Date('2025-08-30T14:31:56'));
        });

        it('handles the exact problematic case from user report', () => {
            // This is the exact input string that was causing issues
            const customRange = '2025-08-30T14:31:08-2025-08-30T14:31:56';
            const result = parseTimeParameter(customRange);

            // Should parse two different timestamps, not the same timestamp twice
            expect(result.start.getTime()).not.toEqual(result.end.getTime());

            // Should parse the correct start time
            expect(result.start).toEqual(new Date('2025-08-30T14:31:08'));

            // Should parse the correct end time
            expect(result.end).toEqual(new Date('2025-08-30T14:31:56'));

            // End should be 48 seconds after start
            expect(result.end.getTime() - result.start.getTime()).toEqual(48000);
        });
    });
});
