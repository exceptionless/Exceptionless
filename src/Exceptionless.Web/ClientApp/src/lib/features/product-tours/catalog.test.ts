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
    const versions = Object.fromEntries(productTourCatalog.map((tour) => [tour.name, 1]));

    it('contains only durable metadata for the five named tours', () => {
        expect(productTourCatalog.map((tour) => tour.name)).toEqual([
            'ui-overview',
            'configure-project',
            'create-saved-view',
            'investigate-error',
            'meet-exie'
        ]);
        expect(productTourCatalog.every((tour) => tour.keywords.length > 0)).toBe(true);
        expect(JSON.stringify(productTourCatalog)).not.toContain('data-tour');
    });

    it('recommends setup until an organization has configured projects', () => {
        expect(getRecommendedProductTourName(context({ organizationId: undefined }))).toBe('configure-project');
        expect(getRecommendedProductTourName(context({ projects: [{ is_configured: false }] }))).toBe('configure-project');
        expect(getRecommendedProductTourName(context({ projects: [{ is_configured: true }] }))).toBe('ui-overview');
    });

    it('reports availability separately from catalog metadata', () => {
        const items = getProductTourItems(
            context({
                assistantAccess: { enabled: false, has_access: false, upgrade_required: false },
                errorEventAvailability: 'empty'
            }),
            versions
        );
        expect(items.find((item) => item.name === 'meet-exie')?.currentAvailability.available).toBe(false);
        expect(items.find((item) => item.name === 'investigate-error')?.currentAvailability.available).toBe(false);
    });

    it('uses server versions as the availability boundary', () => {
        const items = getProductTourItems(context(), {});
        expect(items.every((item) => !item.currentAvailability.available && item.version === 0)).toBe(true);
    });
});
