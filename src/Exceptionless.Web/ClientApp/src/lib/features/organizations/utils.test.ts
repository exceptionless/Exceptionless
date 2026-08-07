import { describe, expect, it } from 'vitest';

import type { ViewOrganization } from './models';

import { getEffectiveEventLimit } from './utils';

function organization(overrides: Partial<ViewOrganization>): ViewOrganization {
    return { bonus_events_per_month: 0, max_events_per_month: 1000, usage: [], ...overrides } as ViewOrganization;
}

describe('getEffectiveEventLimit', () => {
    it('uses the current usage limit so active bonus events match backend enforcement', () => {
        const now = new Date();
        const usageDate = new Date(Date.UTC(now.getUTCFullYear(), now.getUTCMonth(), 1));
        const value = organization({
            bonus_events_per_month: 250,
            max_events_per_month: 1000,
            usage: [{ blocked: 0, date: usageDate.toISOString(), deleted: 0, discarded: 0, limit: 1250, too_big: 0, total: 0 }]
        });

        expect(getEffectiveEventLimit(value)).toBe(1250);
    });

    it('preserves unlimited organizations', () => {
        expect(getEffectiveEventLimit(organization({ max_events_per_month: -1 }))).toBe(-1);
    });

    it('treats legacy zero-limit organizations as unlimited', () => {
        expect(getEffectiveEventLimit(organization({ max_events_per_month: 0 }))).toBe(-1);
    });
});
