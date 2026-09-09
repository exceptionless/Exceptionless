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
        expect(shouldOfferProductTourInvitation(undefined)).toBe(true);
    });

    it('does not offer an invitation after it has been recorded', () => {
        expect(shouldOfferProductTourInvitation('2026-09-08T00:00:00Z')).toBe(false);
    });
});
