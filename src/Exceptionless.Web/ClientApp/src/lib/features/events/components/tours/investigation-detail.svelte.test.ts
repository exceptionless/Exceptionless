import { productTourCheckpoint } from '$features/product-tours/state.svelte';
import { cleanup, fireEvent, render, screen } from '@testing-library/svelte';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';

import type { PersistentEvent } from '../../models';

import InvestigationDetailTour from './investigation-detail.svelte';

const actions = vi.hoisted(() => ({ complete: vi.fn(), dismiss: vi.fn() }));
vi.mock('$features/product-tours/actions.svelte', () => ({ createProductTourActions: () => actions }));
vi.mock('$features/product-tours/activity', () => ({ submitProductTourActivity: vi.fn() }));

const event: PersistentEvent = {
    created_utc: '2026-09-01T00:00:00Z',
    data: { '@simple_error': { message: 'Example error', type: 'ExampleException' } },
    date: '2026-09-01T00:00:00Z',
    id: 'event',
    is_first_occurrence: false,
    organization_id: 'organization',
    project_id: 'project',
    stack_id: 'stack',
    type: 'error'
};

describe('InvestigationDetailTour', () => {
    let targets: HTMLElement[];
    beforeEach(() => {
        vi.stubGlobal(
            'ResizeObserver',
            class {
                public disconnect() {}
                public observe() {}
            }
        );
        targets = ['stack-metrics', 'event-overview', 'stack-events'].map((name) => {
            const element = document.createElement('button');
            element.dataset.tour = name;
            element.scrollIntoView = vi.fn();
            document.body.append(element);
            return element;
        });
    });

    afterEach(() => {
        cleanup();
        targets.forEach((target) => target.remove());
        vi.unstubAllGlobals();
        productTourCheckpoint.clear();
        vi.clearAllMocks();
    });

    it('uses a spotlight on the actual control at every detail step', async () => {
        // Arrange
        productTourCheckpoint.start('event-investigate', 'choose-error', 'user');
        const onCompareEvents = vi.fn();
        render(InvestigationDetailTour, { event, onCompareEvents });
        await screen.findByText('See the impact');

        for (const [index, title] of ['See the impact', 'Read what happened', 'See related reports'].entries()) {
            // Act
            await screen.findByText(title);

            // Assert
            expect(targets[index]?.classList.contains('driver-active-element')).toBe(true);
            if (index < targets.length - 1) {
                // Act
                await fireEvent.click(screen.getByRole('button', { name: 'Next' }));
            }
        }

        // Act
        await fireEvent.click(screen.getByRole('button', { name: 'Show all events' }));

        // Assert
        expect(onCompareEvents).toHaveBeenCalledOnce();
        expect(actions.dismiss).not.toHaveBeenCalled();
    });

    it('records completion when the highlighted Show all events control is used', async () => {
        // Arrange
        const checkpoint = productTourCheckpoint.start('event-investigate', 'filter-stack-events', 'user');
        const view = render(InvestigationDetailTour, { event, onCompareEvents: vi.fn() });

        // Act: the parent calls this before closing the details panel.
        await view.component.completeComparison();

        // Assert
        expect(actions.complete).toHaveBeenCalledExactlyOnceWith(checkpoint);
    });

    it('does not advance for a non-error event', () => {
        // Arrange
        productTourCheckpoint.start('event-investigate', 'choose-error', 'user');

        // Act
        render(InvestigationDetailTour, { event: { ...event, data: {}, type: 'log' }, onCompareEvents: vi.fn() });

        // Assert
        expect(productTourCheckpoint.current?.checkpointName).toBe('choose-error');
        expect(screen.queryByRole('region', { name: 'Guide' })).toBeNull();
    });

    it('advances an already-open error after selection and on a later guide run', async () => {
        // Arrange
        productTourCheckpoint.start('event-investigate', 'choose-error', 'user');
        render(InvestigationDetailTour, { event, onCompareEvents: vi.fn() });

        for (let run = 0; run < 2; run++) {
            // Act
            productTourCheckpoint.start('event-investigate', 'choose-error', 'user');
            await screen.findByText('See the impact');

            // Assert
            expect(productTourCheckpoint.current?.checkpointName).toBe('stack-summary');
        }
    });

    it('goes back through detail steps without reopening events or saving progress', async () => {
        // Arrange
        productTourCheckpoint.start('event-investigate', 'tab-overview', 'user');
        render(InvestigationDetailTour, { event, onCompareEvents: vi.fn() });

        // Act
        await fireEvent.click(await screen.findByRole('button', { name: 'Back' }));
        await screen.findByText('See the impact');

        // Assert
        expect(productTourCheckpoint.current?.checkpointName).toBe('stack-summary');
        expect(screen.queryByRole('button', { name: 'Back' })).toBeNull();
        expect(actions.complete).not.toHaveBeenCalled();
        expect(actions.dismiss).not.toHaveBeenCalled();
    });

    it('retains an accessible end-guide action', async () => {
        // Arrange
        productTourCheckpoint.start('event-investigate', 'stack-summary', 'user');
        render(InvestigationDetailTour, { event, onCompareEvents: vi.fn() });

        // Act
        await fireEvent.click(await screen.findByRole('button', { name: 'End guide' }));

        // Assert
        expect(actions.dismiss).toHaveBeenCalledExactlyOnceWith(productTourCheckpoint.current);
        expect(actions.complete).not.toHaveBeenCalled();
    });
});
