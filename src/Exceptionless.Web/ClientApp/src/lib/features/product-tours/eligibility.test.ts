import { ProductTourStatus } from '$generated/api';
import { describe, expect, it } from 'vitest';

import { isProductTourSetupRoute, shouldOfferProductTourAnnouncement, shouldOfferProductTourWelcome } from './eligibility';

describe('product tour setup routes', () => {
    it.each(['/(app)/organization/add', '/(app)/project/add', '/(app)/project/[projectId]/configure'])('suppresses automatic tours on %s', (routeId) => {
        expect(isProductTourSetupRoute(routeId)).toBe(true);
    });

    it('allows automatic tours after setup', () => {
        expect(isProductTourSetupRoute('/(app)/stack')).toBe(false);
        expect(isProductTourSetupRoute(null)).toBe(false);
    });
});

describe('product tour welcome eligibility', () => {
    it('offers legacy users and a newer welcome version', () => {
        expect(shouldOfferProductTourWelcome(undefined, 1)).toBe(true);
        expect(shouldOfferProductTourWelcome({ status: ProductTourStatus.Completed, version: 1 }, 2)).toBe(true);
    });

    it('suppresses both explicit Start and Skip outcomes for the current version', () => {
        expect(shouldOfferProductTourWelcome({ status: ProductTourStatus.Completed, version: 1 }, 1)).toBe(false);
        expect(shouldOfferProductTourWelcome({ status: ProductTourStatus.Dismissed, version: 1 }, 1)).toBe(false);
    });
});

describe('product tour feature announcement eligibility', () => {
    it('offers a new announcement version until explicitly recorded', () => {
        expect(shouldOfferProductTourAnnouncement(undefined, 1)).toBe(true);
        expect(shouldOfferProductTourAnnouncement({ status: ProductTourStatus.Dismissed, version: 1 }, 1)).toBe(false);
        expect(shouldOfferProductTourAnnouncement({ status: ProductTourStatus.Completed, version: 2 }, 1)).toBe(false);
    });
});
