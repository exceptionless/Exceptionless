import { ProductTourStatus } from '$generated/api';
import { describe, expect, it } from 'vitest';

import { shouldOfferProductTourAnnouncement, shouldOfferProductTourWelcome } from './eligibility';

describe('product tour welcome eligibility', () => {
    it('offers legacy users and a newer welcome version', () => {
        expect(shouldOfferProductTourWelcome(undefined, 1)).toBe(true);
        expect(shouldOfferProductTourWelcome({ status: ProductTourStatus.Completed, updated_utc: '', version: 1 }, 2)).toBe(true);
    });

    it('suppresses both explicit Start and Skip outcomes for the current version', () => {
        expect(shouldOfferProductTourWelcome({ status: ProductTourStatus.Completed, updated_utc: '', version: 1 }, 1)).toBe(false);
        expect(shouldOfferProductTourWelcome({ status: ProductTourStatus.Dismissed, updated_utc: '', version: 1 }, 1)).toBe(false);
    });
});

describe('product tour feature announcement eligibility', () => {
    it('offers a new announcement version until explicitly recorded', () => {
        expect(shouldOfferProductTourAnnouncement(undefined, 1)).toBe(true);
        expect(shouldOfferProductTourAnnouncement({ status: ProductTourStatus.Dismissed, updated_utc: '', version: 1 }, 1)).toBe(false);
        expect(shouldOfferProductTourAnnouncement({ status: ProductTourStatus.Completed, updated_utc: '', version: 2 }, 1)).toBe(false);
    });
});
