import { describe, expect, it } from 'vitest';

import { savedFilterUsesPremiumFeatures } from './premium-filter';

describe('savedFilterUsesPremiumFeatures', () => {
    it('preserves a stored premium classification', () => {
        expect(savedFilterUsesPremiumFeatures('status:open', true)).toBe(true);
    });

    it('classifies legacy saved views from their filter', () => {
        expect(savedFilterUsesPremiumFeatures('data.customer_id:42', false)).toBe(true);
        expect(savedFilterUsesPremiumFeatures('idx.customer_id:42', undefined)).toBe(true);
    });

    it('keeps legacy saved views with free filters free', () => {
        expect(savedFilterUsesPremiumFeatures('status:open type:error', false)).toBe(false);
    });
});
