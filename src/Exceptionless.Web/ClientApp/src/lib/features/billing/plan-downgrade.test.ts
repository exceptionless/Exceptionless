import { describe, expect, it } from 'vitest';

import { getDowngradeLosses, type PlanLimits } from './plan-downgrade';

const trial: PlanLimits = { has_premium_features: true, max_events_per_month: 75000, retention_days: 90 };
const free: PlanLimits = { has_premium_features: false, max_events_per_month: 3000, retention_days: 3 };

describe('getDowngradeLosses', () => {
    it('lists retention, event limit, and premium losses when moving to free', () => {
        // Act
        const losses = getDowngradeLosses(trial, free);

        // Assert
        expect(losses).toEqual(['event history drops from 90 days to 3 days', 'limit drops to 3,000 events a month', 'premium features turn off']);
    });

    it('treats moving from unlimited events to a limit as a loss', () => {
        // Arrange
        const unlimited: PlanLimits = { ...trial, max_events_per_month: -1 };

        // Act
        const losses = getDowngradeLosses(unlimited, trial);

        // Assert
        expect(losses).toEqual(['limit drops to 75,000 events a month']);
    });

    it('returns nothing when no limit goes down', () => {
        // Act
        const losses = getDowngradeLosses(free, trial);

        // Assert
        expect(losses).toEqual([]);
    });
});
