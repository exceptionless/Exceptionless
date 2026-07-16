import { describe, expect, it } from 'vitest';

import { getBudgetThresholdEventCount, parseBudgetThresholds } from './budget-utils';
import { BudgetAlertCardSchema } from './schemas';

describe('organization budget controls', () => {
    it('normalizes, sorts, and deduplicates threshold input', () => {
        expect(parseBudgetThresholds('80, 50, 80, 90')).toEqual([50, 80, 90]);
        expect(parseBudgetThresholds('')).toEqual([]);
        expect(parseBudgetThresholds('  ,  ')).toEqual([]);
    });

    it('rejects decimals, out-of-range thresholds, and empty enabled settings', () => {
        expect(BudgetAlertCardSchema.safeParse({ enabled: true, thresholds: '' }).success).toBe(false);
        expect(BudgetAlertCardSchema.safeParse({ enabled: false, thresholds: '50.5' }).success).toBe(false);
        expect(BudgetAlertCardSchema.safeParse({ enabled: false, thresholds: '100' }).success).toBe(false);
    });

    it('calculates threshold event counts and disables percentages for unlimited plans', () => {
        expect(getBudgetThresholdEventCount(1001, 50)).toBe(501);
        expect(getBudgetThresholdEventCount(-1, 50)).toBeNull();
        expect(getBudgetThresholdEventCount(1001, 50.5)).toBeNull();
    });
});
