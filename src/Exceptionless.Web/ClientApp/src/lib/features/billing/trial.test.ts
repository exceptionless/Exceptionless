import { describe, expect, it } from 'vitest';

import { getTrialDaysRemaining, getTrialEndedNoticeKey, hasTrialEndedWithoutSubscribing, isTrialEndingSoon } from './trial.ts';

describe('getTrialDaysRemaining', () => {
    it.each([
        ['a full trial', '2026-11-05T12:00:00Z', 14],
        ['part of a day', '2026-10-22T18:00:00Z', 1],
        ['an ended trial', '2026-10-21T12:00:00Z', 0],
        ['no trial', null, 0],
        ['an invalid date', 'not-a-date', 0]
    ])('returns the days left for %s', (...testCase) => {
        // Arrange
        const [, trialEndsUtc, expected] = testCase;
        const now = new Date('2026-10-22T12:00:00Z');

        // Act
        const daysRemaining = getTrialDaysRemaining(trialEndsUtc, now);

        // Assert
        expect(daysRemaining).toBe(expected);
    });
});

describe('hasTrialEndedWithoutSubscribing', () => {
    it.each([
        ['a trial that ended without subscribing', { subscribe_date: null, trial_ends_utc: '2026-10-21T12:00:00Z' }, true],
        ['a trial that converted and later downgraded', { subscribe_date: '2026-10-20T12:00:00Z', trial_ends_utc: '2026-10-21T12:00:00Z' }, false],
        ['a legacy empty subscribe date', { subscribe_date: '0001-01-01T00:00:00', trial_ends_utc: '2026-10-21T12:00:00Z' }, true],
        ['an organization that never had a trial', { subscribe_date: null, trial_ends_utc: null }, false]
    ])('returns the result for %s', (...testCase) => {
        // Arrange
        const [, organization, expected] = testCase;

        // Act
        const trialEnded = hasTrialEndedWithoutSubscribing(organization);

        // Assert
        expect(trialEnded).toBe(expected);
    });
});

describe('isTrialEndingSoon', () => {
    it.each([
        [14, false],
        [4, false],
        [3, true],
        [1, true],
        [0, true]
    ])('returns whether %i days left is the end of the trial', (...testCase) => {
        // Arrange
        const [daysRemaining, expected] = testCase;

        // Act
        const endingSoon = isTrialEndingSoon(daysRemaining);

        // Assert
        expect(endingSoon).toBe(expected);
    });
});

describe('getTrialEndedNoticeKey', () => {
    it('returns a product tour record name the API accepts', () => {
        // Arrange
        const organizationId = '5f8e1b2c3d4e5f6a7b8c9d0e';

        // Act
        const key = getTrialEndedNoticeKey(organizationId);

        // Assert
        expect(key).toBe('trial-ended-5f8e1b2c3d4e5f6a7b8c9d0e');
        expect(key).toMatch(/^[a-z0-9-]{1,64}$/);
    });
});
