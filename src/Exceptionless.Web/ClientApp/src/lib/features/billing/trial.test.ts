import { describe, expect, it } from 'vitest';

import { getTrialDaysRemaining } from './trial.ts';

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
