import { describe, expect, it } from 'vitest';

import type { ProductTourContext } from './types';

import { getProductTourItems, getRecommendedProductTourId, productTourCatalog } from './catalog';

function context(overrides: Partial<ProductTourContext> = {}): ProductTourContext {
    return {
        isSetupPage: false,
        organizationId: 'organization-id',
        pathname: '/next/event',
        projects: [],
        ...overrides
    };
}

describe('product tour catalog', () => {
    it('contains the five stable, versioned guides with unique step ids', () => {
        expect(productTourCatalog.map((tour) => tour.id)).toEqual([
            'new-ui-overview',
            'configure-project',
            'create-saved-view',
            'investigate-error',
            'meet-exie'
        ]);

        for (const tour of productTourCatalog) {
            expect(tour.version).toBeGreaterThan(0);
            expect(tour.keywords.length).toBeGreaterThan(0);
            const steps = tour.getSteps(context());
            expect(new Set(steps.map((step) => step.id)).size).toBe(steps.length);
            expect(steps.every((step) => (step.anchor ? step.anchor.length > 0 : true))).toBe(true);
        }
    });

    it('recommends setup without an organization or with an unconfigured project', () => {
        expect(getRecommendedProductTourId(context({ organizationId: undefined }))).toBe('configure-project');
        expect(getRecommendedProductTourId(context({ projects: [{ is_configured: false } as never] }))).toBe('configure-project');
        expect(getRecommendedProductTourId(context({ projects: [{ is_configured: true } as never] }))).toBe('new-ui-overview');
    });

    it('returns concrete availability reasons while retaining unavailable guides in the catalog', () => {
        const items = getProductTourItems(context({ assistantAccess: { enabled: false } as never, organizationId: undefined }));
        const exie = items.find((item) => item.id === 'meet-exie');
        const investigate = items.find((item) => item.id === 'investigate-error');

        expect(exie?.availability).toEqual({ available: false, reason: 'Exie is not enabled by this Exceptionless installation.' });
        expect(investigate?.availability.available).toBe(false);
        expect(investigate?.availability.reason).toBeTruthy();
    });
});
