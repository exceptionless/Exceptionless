import { describe, expect, it } from 'vitest';

import { getGuideOutcomeRate, getProductTourUsageParams, getRate, getStartSourceShare } from './product-tour-usage';

describe('product tour usage helpers', () => {
    it('maps each immutable range to the API query parameters', () => {
        expect(getProductTourUsageParams({ kind: 'month', month: '2026-08' })).toEqual({ month: '2026-08-01' });
        expect(getProductTourUsageParams({ kind: 'history' })).toEqual({ history: true });
    });

    it('returns null when a percentage has no denominator', () => {
        expect(getRate(1, 0)).toBeNull();
        expect(getGuideOutcomeRate({ completed: 0, dismissed: 0, started: 0 }, 'completed')).toBeNull();
        expect(getStartSourceShare({ count: 1 }, 0)).toBeNull();
    });

    it('calculates prompt and guide rates from their declared denominators', () => {
        expect(getRate(3, 4)).toBe(0.75);
        expect(getGuideOutcomeRate({ completed: 3, dismissed: 1, started: 5 }, 'completed')).toBe(0.6);
        expect(getGuideOutcomeRate({ completed: 3, dismissed: 1, started: 5 }, 'dismissed')).toBe(0.2);
        expect(getStartSourceShare({ count: 2 }, 5)).toBe(0.4);
    });
});
