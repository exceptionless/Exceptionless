import { describe, expect, it } from 'vitest';

import type { ProductTourContext } from './models';

import { getProductTourItems, getRecommendedProductTourName, productTourCatalog } from './catalog';

function context(overrides: Partial<ProductTourContext> = {}): ProductTourContext {
    return {
        errorEventAvailability: 'available',
        isProjectConfigurePage: false,
        isSetupPage: false,
        organizationId: 'organization-id',
        pathname: '/next',
        projects: [],
        ...overrides
    };
}

describe('product tour catalog', () => {
    it('resumes project setup using route identity rather than path substrings', () => {
        // Arrange
        const guide = productTourCatalog.find((tour) => tour.name === 'project-configure')!;

        // Act / Assert
        expect(guide.canResume('organization-name', '/(app)/organization/add')).toBe(true);
        expect(guide.canResume('project-name', '/(app)/project/add')).toBe(true);
        expect(guide.canResume('sdk-instructions', '/(app)/project/[projectId]/configure')).toBe(true);
        expect(guide.canResume('sdk-instructions', '/(app)/project/add')).toBe(false);
        expect(guide.canResume('sdk-instructions', null)).toBe(false);
    });

    it('does not resume dialog or detail checkpoints on their parent list', () => {
        // Arrange
        const savedView = productTourCatalog.find((tour) => tour.name === 'saved-view-create')!;
        const investigation = productTourCatalog.find((tour) => tour.name === 'event-investigate')!;

        // Act / Assert
        expect(savedView.canResume('open-view-menu', '/(app)/event')).toBe(true);
        expect(savedView.canResume('name-view', '/(app)/event')).toBe(false);
        expect(investigation.canResume('choose-error', '/(app)/event')).toBe(true);
        expect(investigation.canResume('stack-summary', '/(app)/event')).toBe(false);
    });

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

    it('maps every guide to a stable record and typed state field', () => {
        expect(getProductTourItems(context()).every((item) => item.stateKey)).toBe(true);
    });

    it('does not mistake an unavailable project list for an empty organization', () => {
        // Arrange
        const currentContext = context({ projects: undefined });

        // Act
        const items = getProductTourItems(currentContext);

        // Assert
        expect(items.find((item) => item.name === 'project-configure')?.currentAvailability).toEqual({
            available: false,
            reason: 'Projects could not be loaded. Try again shortly.'
        });
        expect(items.find((item) => item.name === 'app-overview')?.currentAvailability.available).toBe(true);
        expect(items.find((item) => item.name === 'saved-view-create')?.currentAvailability.available).toBe(true);
        expect(getRecommendedProductTourName(currentContext)).toBe('app-overview');
    });

    it.each([{ isProjectConfigurePage: true }, { organizationId: undefined }])('allows setup without a project lookup when %o', (overrides) => {
        // Arrange
        const currentContext = context({ projects: undefined, ...overrides });

        // Act
        const projectGuide = getProductTourItems(currentContext).find((item) => item.name === 'project-configure');

        // Assert
        expect(projectGuide?.currentAvailability.available).toBe(true);
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
            isProjectConfigurePage: true,
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

    it('keeps Client Setup when the organization project list has not caught up', () => {
        // Arrange
        const definition = productTourCatalog.find((tour) => tour.name === 'project-configure')!;

        // Act
        const start = definition.start(
            context({
                isProjectConfigurePage: true,
                organizationId: undefined,
                pathname: '/next/project/current-project/configure',
                projects: [],
                search: '?type=dotnet-legacy-mvc'
            })
        );

        // Assert
        expect(start).toEqual({ checkpointName: 'choose-platform', route: '/next/project/current-project/configure?type=dotnet-legacy-mvc&redirect=true' });
    });

    it('requires actual Exie access', () => {
        const item = getProductTourItems(context({ assistantAccess: { enabled: true, has_access: false, upgrade_required: true } })).find(
            (tour) => tour.name === 'exie-overview'
        );
        expect(item?.currentAvailability.available).toBe(false);
    });
});
