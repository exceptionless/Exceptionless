import { ProductTourUsageInterval } from '$generated/api';
import { describe, expect, it } from 'vitest';

import { getProductTourActivity, getProductTourUsageParams } from './product-tour-usage';

describe('product tour usage helpers', () => {
    it('maps each immutable range to the API query parameters', () => {
        expect(getProductTourUsageParams({ kind: 'month', month: '2026-08' })).toEqual({ month: '2026-08-01' });
        expect(getProductTourUsageParams({ kind: 'history' })).toEqual({ history: true });
    });

    it('fills missing UTC days without inventing future activity', () => {
        // Arrange
        const activity = [{ completed: 0, date_utc: '2026-08-02T00:00:00Z', dismissed: 0, shown: 2, started: 1 }];

        // Act
        const data = getProductTourActivity(
            activity,
            ProductTourUsageInterval.Day,
            '2026-08-01T00:00:00Z',
            '2026-09-01T00:00:00Z',
            new Date('2026-08-03T12:00:00Z')
        );

        // Assert
        expect(data).toHaveLength(3);
        expect(data.map((period) => period.shown)).toEqual([0, 2, 0]);
        expect(data[0]?.date.toISOString()).toBe('2026-08-01T00:00:00.000Z');
    });

    it('keeps monthly history buckets at UTC month starts', () => {
        // Arrange
        const activity = [{ completed: 3, date_utc: '2026-02-01T00:00:00Z', dismissed: 1, shown: 0, started: 5 }];

        // Act
        const data = getProductTourActivity(
            activity,
            ProductTourUsageInterval.Month,
            '2026-01-31T15:00:00Z',
            '2026-04-01T00:00:00Z',
            new Date('2026-05-01T00:00:00Z')
        );

        // Assert
        expect(data.map((period) => period.date.getUTCMonth())).toEqual([0, 1, 2]);
        expect(data.map((period) => period.started)).toEqual([0, 5, 0]);
    });

    it('does not invent a start date for empty unlimited history', () => {
        // Act
        const data = getProductTourActivity([], ProductTourUsageInterval.Month, null, '2026-04-01T00:00:00Z');

        // Assert
        expect(data).toEqual([]);
    });
});
