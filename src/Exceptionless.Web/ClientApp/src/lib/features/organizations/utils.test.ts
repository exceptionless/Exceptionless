import { describe, expect, it } from 'vitest';

import type { ViewOrganization } from './models';

import { getEffectiveEventLimit, getUtcMonthKey } from './utils';

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

describe('getUtcMonthKey', () => {
    it('changes only when the UTC month changes', () => {
        expect(getUtcMonthKey(new Date('2026-08-01T00:00:00.000Z'))).toBe(getUtcMonthKey(new Date('2026-08-31T23:59:59.999Z')));
        expect(getUtcMonthKey(new Date('2026-09-01T00:00:00.000Z'))).not.toBe(getUtcMonthKey(new Date('2026-08-31T23:59:59.999Z')));
    });
});
