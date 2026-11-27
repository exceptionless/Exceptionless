import { describe, expect, it } from 'vitest';

import { getDowngradeLosses, type PlanLimits } from './plan-downgrade';

const trial: PlanLimits = { has_assistant: true, has_premium_features: true, max_events_per_month: 75000, retention_days: 90 };
const free: PlanLimits = { has_assistant: false, has_premium_features: false, max_events_per_month: 3000, retention_days: 3 };

describe('getDowngradeLosses', () => {
    it.each([
        {
            current: trial,
            expected: ['event history drops from 90 days to 3 days', 'limit drops to 3,000 events a month', 'Exie turns off', 'premium features turn off'],
            name: 'lists retention, event limit, Exie, and premium losses when moving to free',
            next: free
        },
        {
            current: { ...trial, max_events_per_month: -1 },
            expected: ['limit drops to 75,000 events a month'],
            name: 'treats moving from unlimited events to a limit as a loss',
            next: trial
        },
        {
            current: trial,
            expected: ['event history drops from 90 days to 30 days', 'limit drops to 15,000 events a month', 'Exie turns off'],
            name: 'lists Exie when the next plan does not include it',
            next: { has_assistant: false, has_premium_features: true, max_events_per_month: 15000, retention_days: 30 }
        },
        { current: free, expected: [], name: 'returns nothing when no limit goes down', next: trial }
    ])('$name', (testCase) => {
        // Arrange
        const { current, expected, next } = testCase;

        // Act
        const losses = getDowngradeLosses(current, next);

        // Assert
        expect(losses).toEqual(expected);
    });
});
