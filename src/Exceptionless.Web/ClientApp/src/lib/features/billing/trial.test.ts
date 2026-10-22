import { describe, expect, it } from 'vitest';

import { getTrialDaysRemaining, hasTrialEndedWithoutSubscribing } from './trial.ts';

describe('getTrialDaysRemaining', () => {
    const now = new Date('2026-10-22T12:00:00Z');

    it.each([
        ['a full trial', '2026-10-29T12:00:00Z', 7],
        ['part of a day', '2026-10-22T18:00:00Z', 1],
        ['an ended trial', '2026-10-21T12:00:00Z', 0],
        ['no trial', null, 0],
        ['an invalid date', 'not-a-date', 0]
    ])('returns the days left for %s', (_, trialEndsUtc, expected) => {
        expect(getTrialDaysRemaining(trialEndsUtc, now)).toBe(expected);
    });
});

describe('hasTrialEndedWithoutSubscribing', () => {
    it.each([
        ['a trial that ended without subscribing', { subscribe_date: null, trial_ends_utc: '2026-10-21T12:00:00Z' }, true],
        ['a trial that converted and later downgraded', { subscribe_date: '2026-10-20T12:00:00Z', trial_ends_utc: '2026-10-21T12:00:00Z' }, false],
        ['a legacy empty subscribe date', { subscribe_date: '0001-01-01T00:00:00', trial_ends_utc: '2026-10-21T12:00:00Z' }, true],
        ['an organization that never had a trial', { subscribe_date: null, trial_ends_utc: null }, false]
    ])('returns the result for %s', (_, organization, expected) => {
        expect(hasTrialEndedWithoutSubscribing(organization)).toBe(expected);
    });
});
