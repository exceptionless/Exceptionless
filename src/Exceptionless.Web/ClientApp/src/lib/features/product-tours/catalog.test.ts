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
    it.each(['filters', 'saved-views'] as const)('only resumes %s where its controls exist', (checkpoint) => {
        const guide = productTourCatalog.find((tour) => tour.name === 'app-overview')!;
        expect(guide.canResume(checkpoint, '/(app)/event')).toBe(true);
        expect(guide.canResume(checkpoint, '/(app)/project/[projectId]/manage')).toBe(false);
        expect(guide.canResume(checkpoint, null)).toBe(false);
    });

    it('allows the View step on every page with a View menu', () => {
        const guide = productTourCatalog.find((tour) => tour.name === 'app-overview')!;
        for (const route of ['/(app)/stack', '/(app)/sessions', '/(app)/stream'] as const) {
            expect(guide.canResume('saved-views', route)).toBe(true);
            expect(guide.canResume('filters', route)).toBe(false);
        }
    });

    it('keeps shell-wide overview steps resumable outside Events', () => {
        const guide = productTourCatalog.find((tour) => tour.name === 'app-overview')!;
        for (const checkpoint of ['navigation', 'events', 'exie', 'command-search'] as const) {
            expect(guide.canResume(checkpoint, '/(app)/project/[projectId]/manage')).toBe(true);
            expect(guide.canResume(checkpoint, null)).toBe(false);
        }
    });

    it('resumes project setup using route identity rather than path substrings', () => {
        // Arrange
        const guide = productTourCatalog.find((tour) => tour.name === 'project-configure')!;

        // Act
        const organization = guide.canResume('organization-name', '/(app)/organization/add');
        const project = guide.canResume('project-name', '/(app)/project/add');
        const sdk = guide.canResume('sdk-instructions', '/(app)/project/[projectId]/configure');
        const wrongRoute = guide.canResume('sdk-instructions', '/(app)/project/add');
        const missingRoute = guide.canResume('sdk-instructions', null);

        // Assert
        expect(organization).toBe(true);
        expect(project).toBe(true);
        expect(sdk).toBe(true);
        expect(wrongRoute).toBe(false);
        expect(missingRoute).toBe(false);
    });

    it('does not resume dialog or detail checkpoints on their parent list', () => {
        // Arrange
        const savedView = productTourCatalog.find((tour) => tour.name === 'saved-view-create')!;
        const investigation = productTourCatalog.find((tour) => tour.name === 'event-investigate')!;

        // Act
        const viewMenu = savedView.canResume('open-view-menu', '/(app)/event');
        const viewDialog = savedView.canResume('name-view', '/(app)/event');
        const errorList = investigation.canResume('choose-error', '/(app)/event');
        const errorDetail = investigation.canResume('stack-summary', '/(app)/event');

        // Assert
        expect(viewMenu).toBe(true);
        expect(viewDialog).toBe(false);
        expect(errorList).toBe(true);
        expect(errorDetail).toBe(false);
    });

    it('contains only durable metadata for the five named tours', () => {
        // Arrange: the static catalog supplies the guide definitions.

        // Act
        const names = productTourCatalog.map((tour) => tour.name);
        const hasKeywords = productTourCatalog.every((tour) => tour.keywords.length > 0);
        const metadata = JSON.stringify(productTourCatalog);

        // Assert
        expect(names).toEqual(['app-overview', 'project-configure', 'saved-view-create', 'event-investigate', 'exie-overview']);
        expect(hasKeywords).toBe(true);
        expect(metadata).not.toContain('data-tour');
    });

    it('recommends setup until an organization has configured projects', () => {
        // Arrange
        const noOrganization = context({ organizationId: undefined });
        const noProjects = context({ projects: [] });
        const unconfigured = context({ projects: [{ id: 'project-id', is_configured: false }] });
        const configured = context({ projects: [{ id: 'project-id', is_configured: true }] });

        // Act
        const recommendations = [noOrganization, noProjects, unconfigured, configured].map(getRecommendedProductTourName);

        // Assert
        expect(recommendations).toEqual(['project-configure', 'project-configure', 'project-configure', 'app-overview']);
    });

    it('reports availability separately from catalog metadata', () => {
        // Arrange
        const currentContext = context({
            assistantAccess: { enabled: false, has_access: false, upgrade_required: false },
            errorEventAvailability: 'empty'
        });

        // Act
        const items = getProductTourItems(currentContext);

        // Assert
        expect(items.find((item) => item.name === 'exie-overview')?.currentAvailability.available).toBe(false);
        expect(items.find((item) => item.name === 'event-investigate')?.currentAvailability.available).toBe(false);
    });

    it('maps every guide to a stable record and typed state field', () => {
        // Arrange
        const currentContext = context();

        // Act
        const items = getProductTourItems(currentContext);

        // Assert
        expect(items.every((item) => item.stateKey)).toBe(true);
    });

    it('does not mistake an unavailable project list for an empty organization', () => {
        // Arrange
        const currentContext = context({ projects: undefined });

        // Act
        const items = getProductTourItems(currentContext);
        const recommended = getRecommendedProductTourName(currentContext);

        // Assert
        expect(items.find((item) => item.name === 'project-configure')?.currentAvailability).toEqual({
            available: false,
            reason: 'Projects could not be loaded. Try again shortly.'
        });
        expect(items.find((item) => item.name === 'app-overview')?.currentAvailability.available).toBe(true);
        expect(items.find((item) => item.name === 'saved-view-create')?.currentAvailability.available).toBe(true);
        expect(recommended).toBe('app-overview');
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
        // Arrange
        const definition = productTourCatalog.find((tour) => tour.name === 'project-configure')!;

        // Act
        const organization = definition.start(context({ organizationId: undefined }));
        const project = definition.start(context({ projects: [] }));
        const platform = definition.start(context({ projects: [{ id: 'project-id', is_configured: false }] }));

        // Assert
        expect(organization).toEqual({ checkpointName: 'organization-name', route: '/next/organization/add' });
        expect(project).toEqual({ checkpointName: 'project-name', route: '/next/project/add' });
        expect(platform).toEqual({
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
        // Arrange
        const currentContext = context({ assistantAccess: { enabled: true, has_access: false, upgrade_required: true } });

        // Act
        const item = getProductTourItems(currentContext).find((tour) => tour.name === 'exie-overview');

        // Assert
        expect(item?.currentAvailability.available).toBe(false);
    });
});
