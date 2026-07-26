import { describe, expect, it } from 'vitest';

import { filterUsesPremiumFeatures } from './premium-filter';

describe('filterUsesPremiumFeatures', () => {
    it('keeps built-in parent reference navigation available without premium features', () => {
        expect(filterUsesPremiumFeatures('(reference:"parent-id" OR ref.parent:"parent-id")')).toBe(false);
    });

    it('still requires premium features for custom references', () => {
        expect(filterUsesPremiumFeatures('ref.custom:"reference-id"')).toBe(true);
    });
});
