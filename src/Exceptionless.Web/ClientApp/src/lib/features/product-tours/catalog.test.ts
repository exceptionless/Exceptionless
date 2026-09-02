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
        expect(getRecommendedProductTourName(context({ projects: [] }))).toBe('project-configure');
        expect(getRecommendedProductTourName(context({ projects: [{ id: 'project-id', is_configured: false }] }))).toBe('project-configure');
        expect(getRecommendedProductTourName(context({ projects: [{ id: 'project-id', is_configured: true }] }))).toBe('app-overview');
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

    it('starts project setup from domain state', () => {
        const definition = productTourCatalog.find((tour) => tour.name === 'project-configure')!;

        expect(definition.start(context({ organizationId: undefined }))).toEqual({ checkpointName: 'organization-name', route: '/next/organization/add' });
        expect(definition.start(context({ projects: [] }))).toEqual({ checkpointName: 'project-name', route: '/next/project/add' });
        expect(definition.start(context({ projects: [{ id: 'project-id', is_configured: false }] }))).toEqual({
            checkpointName: 'choose-platform',
            route: '/next/project/project-id/configure?redirect=true'
        });
    });

    it('keeps the current project and SDK when starting from Client Setup', () => {
        // Arrange
        const definition = productTourCatalog.find((tour) => tour.name === 'project-configure')!;
        const currentContext = context({
            pathname: '/next/project/current-project/configure',
            projects: [
                { id: 'other-project', is_configured: false },
                { id: 'current-project', is_configured: true }
            ],
            search: '?type=dotnet-legacy-mvc'
        });

        // Act
        const start = definition.start(currentContext);

        // Assert
        expect(start).toEqual({ checkpointName: 'choose-platform', route: '/next/project/current-project/configure?type=dotnet-legacy-mvc&redirect=true' });
    });

    it('does not carry another page SDK selection into project setup', () => {
        // Arrange
        const definition = productTourCatalog.find((tour) => tour.name === 'project-configure')!;

        // Act
        const start = definition.start(context({ projects: [{ id: 'project-id', is_configured: false }], search: '?type=error' }));

        // Assert
        expect(start).toEqual({ checkpointName: 'choose-platform', route: '/next/project/project-id/configure?redirect=true' });
    });

    it('requires actual Exie access', () => {
        const item = getProductTourItems(context({ assistantAccess: { enabled: true, has_access: false, upgrade_required: true } })).find(
            (tour) => tour.name === 'exie-overview'
        );
        expect(item?.currentAvailability.available).toBe(false);
    });
});
