import { describe, expect, it } from 'vitest';

import { resolveInitialPlanTierId } from './plan-selection';

const tiers = ['EX_SMALL', 'EX_MEDIUM', 'EX_LARGE'];

describe('resolveInitialPlanTierId', () => {
    it('uses a requested monthly plan tier for a free organization', () => {
        expect(resolveInitialPlanTierId(tiers, 'EX_MEDIUM', 'EX_FREE', true)).toBe('EX_MEDIUM');
    });

    it('normalizes a requested yearly plan to its tier', () => {
        expect(resolveInitialPlanTierId(tiers, 'EX_MEDIUM_YEARLY', 'EX_SMALL', false)).toBe('EX_MEDIUM');
    });

    it('falls back to the existing generic upsell selection', () => {
        expect(resolveInitialPlanTierId(tiers, 'UNKNOWN', 'EX_MEDIUM', false)).toBe('EX_LARGE');
    });
});
