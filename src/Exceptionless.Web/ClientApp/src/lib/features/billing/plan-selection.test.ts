import { describe, expect, it } from 'vitest';

import { resolveInitialTierId } from './plan-selection';

const tiers = ['EX_SMALL', 'EX_MEDIUM', 'EX_LARGE'];

describe('resolveInitialTierId', () => {
    it('uses the requested eligible tier for a free organization', () => {
        expect(resolveInitialTierId(tiers, 'EX_MEDIUM', 'EX_FREE', true)).toBe('EX_MEDIUM');
    });

    it('uses the requested eligible tier for a small organization', () => {
        expect(resolveInitialTierId(tiers, 'EX_MEDIUM', 'EX_SMALL', false)).toBe('EX_MEDIUM');
    });

    it('falls back to the existing generic upsell selection', () => {
        expect(resolveInitialTierId(tiers, 'UNKNOWN', 'EX_MEDIUM', false)).toBe('EX_LARGE');
    });
});
