import { describe, expect, it } from 'vitest';

import type { ProductTourContext } from './types';

import { getProductTourItems, getRecommendedProductTourId, productTourCatalog } from './catalog';

function context(overrides: Partial<ProductTourContext> = {}): ProductTourContext {
    return {
        errorEventAvailability: 'available',
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

    it('reuses only an open error and otherwise asks before leaving a detail page', () => {
        const investigate = productTourCatalog.find((tour) => tour.id === 'investigate-error')!;

        expect(investigate.getStartAction?.(context({ openEventType: 'error', pathname: '/next/event/error-id' }))).toEqual({
            stepId: 'stack-summary',
            type: 'launch'
        });
        expect(investigate.getStartAction?.(context({ openEventType: 'usage', pathname: '/next/event/usage-id' }))).toMatchObject({
            destination: '/next/event?time=all&type=error',
            type: 'confirm-navigation'
        });
        expect(investigate.getStartAction?.(context({ pathname: '/next/event' }))).toEqual({
            destination: '/next/event?time=all&type=error',
            type: 'navigate'
        });
    });

    it('teaches error filtering, stack triage, occurrence evidence, and only the tabs that are present', () => {
        const investigate = productTourCatalog.find((tour) => tour.id === 'investigate-error')!;
        const steps = investigate.getSteps(context());

        expect(steps.map((step) => step.id)).toEqual([
            'filter-errors',
            'choose-error',
            'stack-summary',
            'stack-triage',
            'event-occurrence',
            'tab-overview',
            'tab-exception',
            'tab-request',
            'tab-environment',
            'tab-trace',
            'tab-session',
            'tab-extended-data',
            'filter-stack-events'
        ]);
        expect(steps.filter((step) => step.id.startsWith('tab-') && step.id !== 'tab-overview').every((step) => step.optional)).toBe(true);
        expect(steps.filter((step) => ['filter-stack-events', 'stack-triage'].includes(step.id)).every((step) => !step.advanceOnClick)).toBe(true);
    });

    it('requires confirmation before setup consumes capacity when every project is configured', () => {
        const configure = productTourCatalog.find((tour) => tour.id === 'configure-project')!;

        expect(configure.getStartAction?.(context({ pathname: '/next/stack', projects: [{ is_configured: true } as never] }))).toMatchObject({
            destination: '/next/project/add',
            type: 'confirm-navigation'
        });
    });
});
