import { describe, expect, it } from 'vitest';

import { getProductTourActivity, getProductTourUsageParams } from './product-tour-usage';

describe('product tour usage helpers', () => {
    it('maps each immutable range to the API query parameters', () => {
        expect(getProductTourUsageParams({ kind: 'month', month: '2026-08' })).toEqual({ end: '2026-09-01T00:00:00.000Z', start: '2026-08-01T00:00:00.000Z' });
        expect(getProductTourUsageParams({ kind: 'history' })).toEqual({});
        expect(getProductTourUsageParams({ days: 30, kind: 'days' }, new Date('2026-03-02T12:00:00Z'))).toEqual({ start: '2026-02-01T00:00:00.000Z' });
    });

    it('preserves exact server buckets across a month boundary, including empty and subdaily periods', () => {
        // Arrange
        const activity = [
            { completed: 0, date_utc: '2026-08-31T12:00:00Z', dismissed: 0, shown: 2, started: 1 },
            { completed: 0, date_utc: '2026-08-31T18:00:00Z', dismissed: 0, shown: 0, started: 0 },
            { completed: 1, date_utc: '2026-09-01T00:00:00Z', dismissed: 0, shown: 3, started: 2 }
        ];

        // Act
        const data = getProductTourActivity(activity, '2026-08-31T12:00:00Z', '2026-09-02T00:00:00Z', new Date('2026-09-03T00:00:00Z'));

        // Assert
        expect(data).toEqual(activity.map((period) => ({ ...period, date: new Date(period.date_utc) })));
    });

    it('trims padding before retained activity and after now without generating new buckets', () => {
        // Arrange
        const activity = [1, 3, 5, 7].map((day) => ({
            completed: 0,
            date_utc: `2026-08-0${day}T00:00:00Z`,
            dismissed: 0,
            shown: 0,
            started: 0
        }));

        // Act
        const data = getProductTourActivity(activity, '2026-08-03T00:00:00Z', '2026-09-01T00:00:00Z', new Date('2026-08-06T12:00:00Z'));

        // Assert
        expect(data.map((period) => period.date_utc)).toEqual(['2026-08-03T00:00:00Z', '2026-08-05T00:00:00Z']);
    });

    it('excludes the upper date boundary', () => {
        // Arrange
        const activity = [
            { completed: 0, date_utc: '2026-02-01T00:00:00Z', dismissed: 0, shown: 0, started: 5 },
            { completed: 0, date_utc: '2026-03-01T00:00:00Z', dismissed: 0, shown: 0, started: 0 }
        ];

        // Act
        const data = getProductTourActivity(activity, null, '2026-03-01T00:00:00Z', new Date('2026-04-01T00:00:00Z'));

        // Assert
        expect(data).toHaveLength(1);
        expect(data[0]?.started).toBe(5);
    });

    it('keeps activity in a histogram bucket that begins before the requested start', () => {
        // Arrange
        const activity = [{ completed: 0, date_utc: '2026-08-03T00:00:00Z', dismissed: 0, shown: 0, started: 1 }];

        // Act
        const data = getProductTourActivity(activity, '2026-08-03T01:00:00Z', '2026-08-04T00:00:00Z', new Date('2026-08-05T00:00:00Z'));

        // Assert
        expect(data[0]?.started).toBe(1);
    });

    it('does not invent a start date for empty unlimited history', () => {
        expect(getProductTourActivity([], null, '2026-04-01T00:00:00Z')).toEqual([]);
    });
});
