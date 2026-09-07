import { describe, expect, it } from 'vitest';

import { getProductTourUsageParams } from './product-tour-usage';

describe('product tour usage date filters', () => {
    it('maps month and retained history to API query parameters', () => {
        expect(getProductTourUsageParams({ kind: 'month', month: '2026-08' })).toEqual({ end: '2026-09-01T00:00:00.000Z', start: '2026-08-01T00:00:00.000Z' });
        expect(getProductTourUsageParams({ kind: 'history' })).toEqual({});
    });

    it.each([
        ['2026-03-02T12:00:00Z', '2026-02-01T00:00:00.000Z'],
        ['2024-03-01T23:59:00Z', '2024-02-01T00:00:00.000Z'],
        ['2026-01-15T00:00:00Z', '2025-12-17T00:00:00.000Z']
    ])('uses calendar days across boundaries from %s', (now, start) => {
        expect(getProductTourUsageParams({ days: 30, kind: 'days' }, new Date(now))).toEqual({ start });
    });
});
