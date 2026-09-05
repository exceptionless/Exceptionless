import { ProductTourStatus } from '$generated/api';
import { describe, expect, it } from 'vitest';

import { isProductTourSetupRoute, shouldOfferProductTourInvitation } from './eligibility';

describe('product tour setup routes', () => {
    it.each(['/(app)/organization/add', '/(app)/project/add', '/(app)/project/[projectId]/configure'])('suppresses automatic tours on %s', (routeId) => {
        expect(isProductTourSetupRoute(routeId)).toBe(true);
    });

    it('allows automatic tours after setup', () => {
        expect(isProductTourSetupRoute('/(app)/stack')).toBe(false);
        expect(isProductTourSetupRoute(null)).toBe(false);
    });
});

describe('product tour invitation eligibility', () => {
    it('offers an invitation when no progress has been saved', () => {
        expect(shouldOfferProductTourInvitation(undefined, 1)).toBe(true);
    });

    it.each([ProductTourStatus.Completed, ProductTourStatus.Dismissed])('only offers a newer invitation after status %s', (status) => {
        // Arrange
        const progress = { status, version: 2 };

        // Act / Assert
        expect(shouldOfferProductTourInvitation(progress, 1)).toBe(false);
        expect(shouldOfferProductTourInvitation(progress, 2)).toBe(false);
        expect(shouldOfferProductTourInvitation(progress, 3)).toBe(true);
    });
});
