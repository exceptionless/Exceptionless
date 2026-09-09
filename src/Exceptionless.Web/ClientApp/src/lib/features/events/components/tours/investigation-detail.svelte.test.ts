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
                disconnect() {}
                observe() {}
            }
        );
        targets = ['stack-metrics', 'stack-status', 'event-occurrence', 'event-overview', 'stack-events'].map((name) => {
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
        render(InvestigationDetailTour, { event });
        await screen.findByText('Understand the grouped issue');

        for (const [index, title] of [
            'Understand the grouped issue',
            'Review the issue status',
            'Inspect the occurrence',
            'Begin with the overview',
            'Compare every occurrence'
        ].entries()) {
            // Act
            await screen.findByText(title);

            // Assert
            expect(targets[index]?.classList.contains('driver-active-element')).toBe(true);
            if (index < targets.length - 1) {
                // Act
                await fireEvent.click(screen.getByRole('button', { name: 'Continue' }));
            }
        }

        // Act
        await fireEvent.click(screen.getByRole('button', { name: 'Finish guide' }));

        // Assert
        expect(actions.complete).toHaveBeenCalledExactlyOnceWith(productTourCheckpoint.current);
        expect(actions.dismiss).not.toHaveBeenCalled();
    });

    it('does not advance for a non-error event', () => {
        // Arrange
        productTourCheckpoint.start('event-investigate', 'choose-error', 'user');

        // Act
        render(InvestigationDetailTour, { event: { ...event, data: {}, type: 'log' } });

        // Assert
        expect(productTourCheckpoint.current?.checkpointName).toBe('choose-error');
        expect(screen.queryByRole('region', { name: 'Guide' })).toBeNull();
    });

    it('advances an already-open error after selection and on a later guide run', async () => {
        // Arrange
        productTourCheckpoint.start('event-investigate', 'filter-errors', 'user');
        render(InvestigationDetailTour, { event });

        for (let run = 0; run < 2; run++) {
            // Act
            productTourCheckpoint.start('event-investigate', 'choose-error', 'user');
            await screen.findByText('Understand the grouped issue');

            // Assert
            expect(productTourCheckpoint.current?.checkpointName).toBe('stack-summary');
        }
    });

    it('goes back through detail steps without reopening events or saving progress', async () => {
        // Arrange
        productTourCheckpoint.start('event-investigate', 'stack-triage', 'user');
        render(InvestigationDetailTour, { event });

        // Act
        await fireEvent.click(await screen.findByRole('button', { name: 'Back' }));
        await screen.findByText('Understand the grouped issue');

        // Assert
        expect(productTourCheckpoint.current?.checkpointName).toBe('stack-summary');
        expect(screen.queryByRole('button', { name: 'Back' })).toBeNull();
        expect(actions.complete).not.toHaveBeenCalled();
        expect(actions.dismiss).not.toHaveBeenCalled();
    });

    it('retains an accessible end-guide action', async () => {
        // Arrange
        productTourCheckpoint.start('event-investigate', 'stack-summary', 'user');
        render(InvestigationDetailTour, { event });

        // Act
        await fireEvent.click(await screen.findByRole('button', { name: 'End guide' }));

        // Assert
        expect(actions.dismiss).toHaveBeenCalledExactlyOnceWith(productTourCheckpoint.current);
        expect(actions.complete).not.toHaveBeenCalled();
    });
});
