import { describe, expect, it } from 'vitest';

import type { ProductTourContext } from './types';

import { getProductTourItems, getRecommendedProductTourName, productTourCatalog } from './catalog';

function context(overrides: Partial<ProductTourContext> = {}): ProductTourContext {
    return {
        errorEventAvailability: 'available',
        isSetupPage: false,
        organizationId: 'organization-id',
        pathname: '/next',
        projects: [],
        ...overrides
    };
}

describe('product tour catalog', () => {
    it('contains only durable metadata for the five named tours', () => {
        expect(productTourCatalog.map((tour) => tour.name)).toEqual([
            'app-overview',
            'project-configure',
            'saved-view-create',
            'event-investigate',
            'exie-overview'
        ]);
        expect(productTourCatalog.every((tour) => tour.keywords.length > 0)).toBe(true);
        expect(JSON.stringify(productTourCatalog)).not.toContain('data-tour');
    });

    it('recommends setup until an organization has configured projects', () => {
        expect(getRecommendedProductTourName(context({ organizationId: undefined }))).toBe('project-configure');
        expect(getRecommendedProductTourName(context({ projects: [{ is_configured: false }] }))).toBe('project-configure');
        expect(getRecommendedProductTourName(context({ projects: [{ is_configured: true }] }))).toBe('app-overview');
    });

    it('reports availability separately from catalog metadata', () => {
        const items = getProductTourItems(
            context({
                assistantAccess: { enabled: false, has_access: false, upgrade_required: false },
                errorEventAvailability: 'empty'
            })
        );
        expect(items.find((item) => item.name === 'exie-overview')?.currentAvailability.available).toBe(false);
        expect(items.find((item) => item.name === 'event-investigate')?.currentAvailability.available).toBe(false);
    });

    it('defines a positive version for every tour', () => {
        expect(getProductTourItems(context()).every((item) => item.version > 0)).toBe(true);
    });
});
