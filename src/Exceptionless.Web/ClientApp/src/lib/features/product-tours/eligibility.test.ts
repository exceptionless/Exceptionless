import { describe, expect, it } from 'vitest';

import { isProductTourSetupRoute, shouldOfferProductTourInvitation } from './eligibility';

describe('product tour setup routes', () => {
    it.each(['/(app)/organization/add', '/(app)/project/add', '/(app)/project/[projectId]/configure'] as const)(
        'suppresses automatic tours on %s',
        (routeId) => {
            // Arrange: each case supplies a setup route ID.

            // Act
            const isSetup = isProductTourSetupRoute(routeId);

            // Assert
            expect(isSetup).toBe(true);
        }
    );

    it('allows automatic tours after setup', () => {
        // Arrange
        const routeId = '/(app)/stack';

        // Act
        const isSetup = isProductTourSetupRoute(routeId);
        const missingRouteIsSetup = isProductTourSetupRoute(null);

        // Assert
        expect(isSetup).toBe(false);
        expect(missingRouteIsSetup).toBe(false);
    });
});

describe('product tour invitation eligibility', () => {
    it('offers an invitation when no progress has been saved', () => {
        // Arrange
        const recordedUtc = undefined;

        // Act
        const eligible = shouldOfferProductTourInvitation(recordedUtc);

        // Assert
        expect(eligible).toBe(true);
    });

    it('does not offer an invitation after it has been recorded', () => {
        // Arrange
        const recordedUtc = '2026-09-08T00:00:00Z';

        // Act
        const eligible = shouldOfferProductTourInvitation(recordedUtc);

        // Assert
        expect(eligible).toBe(false);
    });
});
