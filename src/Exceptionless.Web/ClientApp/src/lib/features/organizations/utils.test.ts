import { describe, expect, it } from 'vitest';

import type { ViewOrganization } from './models';

import { getMonthlyEventUsage, getUtcMonthKey, isApproachingMonthlyEventLimit } from './utils';

describe('getUtcMonthKey', () => {
    it('changes only when the UTC month changes', () => {
        expect(getUtcMonthKey(new Date('2026-08-01T00:00:00.000Z'))).toBe(getUtcMonthKey(new Date('2026-08-31T23:59:59.999Z')));
        expect(getUtcMonthKey(new Date('2026-09-01T00:00:00.000Z'))).not.toBe(getUtcMonthKey(new Date('2026-08-31T23:59:59.999Z')));
    });
});

describe('getMonthlyEventUsage', () => {
    const now = new Date('2026-10-22T12:00:00Z');

    function organizationWithUsage(usage: Partial<ViewOrganization['usage'][number]>[]): ViewOrganization {
        return {
            usage: usage.map((entry) => ({ blocked: 0, date: '2026-10-01T00:00:00Z', deleted: 0, discarded: 0, limit: 3000, too_big: 0, total: 0, ...entry }))
        } as ViewOrganization;
    }

    it('returns the current month usage against the limit', () => {
        expect(getMonthlyEventUsage(organizationWithUsage([{ total: 2400 }]), now)).toEqual({ limit: 3000, percent: 0.8, total: 2400 });
    });

    it('ignores usage from an earlier month', () => {
        expect(getMonthlyEventUsage(organizationWithUsage([{ date: '2026-09-01T00:00:00Z', total: 2900 }]), now)).toBeUndefined();
    });

    it('ignores unlimited plans', () => {
        expect(getMonthlyEventUsage(organizationWithUsage([{ limit: -1, total: 2900 }]), now)).toBeUndefined();
    });

    it.each([
        [2399, false],
        [2400, true],
        [3000, true]
    ])('warns at 80%% of the limit (%i of 3000 events: %s)', (total, expected) => {
        expect(isApproachingMonthlyEventLimit(organizationWithUsage([{ total }]), now)).toBe(expected);
    });
});
