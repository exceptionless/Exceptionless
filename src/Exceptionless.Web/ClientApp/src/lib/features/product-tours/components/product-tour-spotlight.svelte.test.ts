import { cleanup, fireEvent, render, screen, waitFor } from '@testing-library/svelte';
import { tick } from 'svelte';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';

import type { ProductTourCheckpoint } from '../models';

import { productTourCheckpoint, productTourPresentation } from '../state.svelte';
import ProductTourSpotlight from './product-tour-spotlight.svelte';
vi.mock('../activity', () => ({ submitProductTourActivity: vi.fn() }));

const checkpoint: ProductTourCheckpoint = { checkpointName: 'command-search', tourName: 'app-overview', userId: 'user' };

describe('ProductTourSpotlight', () => {
    let target: HTMLButtonElement;

    beforeEach(() => {
        vi.stubGlobal(
            'ResizeObserver',
            class {
                public disconnect() {}
                public observe() {}
            }
        );
        target = document.createElement('button');
        target.scrollIntoView = vi.fn();
        document.body.append(target);
    });

    afterEach(() => {
        cleanup();
        productTourCheckpoint.clear();
        productTourPresentation.suspended = false;
        target.remove();
        vi.unstubAllGlobals();
    });

    it('renders safe text and progress in the driver popover', async () => {
        // Arrange: beforeEach creates the spotlight target.

        // Act
        render(ProductTourSpotlight, {
            props: {
                checkpoint,
                description: 'Search <not markup>',
                onDismiss: vi.fn(async () => true),
                target,
                title: 'Search'
            }
        });

        // Assert
        expect(await screen.findByText('Search <not markup>')).toBeTruthy();
        expect(screen.queryByRole('button', { name: 'Back' })).toBeNull();
        expect(screen.getByText('Step 6 of 6')).toBeTruthy();

        // Act
        cleanup();

        // Assert
        expect(document.querySelector('.product-tour-popover')).toBeNull();
    });

    it('omits progress when checkpoints include work outside the guide', async () => {
        // Arrange: beforeEach creates the spotlight target.

        // Act
        render(ProductTourSpotlight, {
            props: { checkpoint, description: 'Choose a platform', onDismiss: vi.fn(async () => true), showProgress: false, target, title: 'Setup' }
        });

        // Assert
        expect(await screen.findByText('Choose a platform')).toBeTruthy();
        expect(screen.queryByText(/Step \d of \d/)).toBeNull();
        expect(screen.getByRole('button', { name: 'End guide' })).toBeTruthy();
    });

    it('enables Back only when the caller provides a safe previous step', async () => {
        // Arrange
        const onPrevious = vi.fn();
        const onNext = vi.fn();
        render(ProductTourSpotlight, {
            props: { checkpoint, description: 'Search', onDismiss: vi.fn(async () => true), onNext, onPrevious, target, title: 'Search' }
        });
        const back = await screen.findByRole('button', { name: 'Back' });

        // Act
        await fireEvent.click(back);

        // Assert
        expect(back.hasAttribute('disabled')).toBe(false);
        expect(onPrevious).toHaveBeenCalledExactlyOnceWith(checkpoint);
        expect(onNext).not.toHaveBeenCalled();
    });

    it('ignores Escape keyup from a closing overlay but handles a fresh Escape press', async () => {
        // Arrange
        const onDismiss = vi.fn(async () => true);
        render(ProductTourSpotlight, { props: { checkpoint, description: 'Search', onDismiss, target, title: 'Search' } });

        // Act
        await fireEvent.keyUp(window, { key: 'Escape' });

        // Assert
        expect(onDismiss).not.toHaveBeenCalled();

        // Act
        await fireEvent.keyDown(window, { key: 'Escape' });

        // Assert
        expect(onDismiss).toHaveBeenCalledExactlyOnceWith(checkpoint);
    });

    it('follows a moving target without a resize event', async () => {
        // Arrange
        let top = 80;
        vi.spyOn(target, 'getBoundingClientRect').mockImplementation(() => new DOMRect(100, top, 100, 32));
        render(ProductTourSpotlight, {
            props: { checkpoint, description: 'Search', onDismiss: vi.fn(async () => true), side: 'bottom', target, title: 'Search' }
        });
        await screen.findByText('Search', { selector: '.driver-popover-title' });
        await new Promise(requestAnimationFrame);
        await new Promise(requestAnimationFrame);
        const popover = document.querySelector<HTMLElement>('.product-tour-popover')!;
        const originalPosition = popover.style.bottom;
        const originalHighlight = document.querySelector('.driver-overlay path')?.getAttribute('d');

        // Act: opening a menu moves its target without changing its size.
        top = 220;

        // Assert
        await waitFor(() => expect(popover.style.bottom).not.toBe(originalPosition));
        expect(document.querySelector('.driver-overlay path')?.getAttribute('d')).not.toBe(originalHighlight);
    });

    it('reattaches when a refreshed list replaces the target element', async () => {
        // Arrange
        target.dataset.tour = 'report';
        render(ProductTourSpotlight, {
            props: { checkpoint, description: 'Open this report', onDismiss: vi.fn(async () => true), target: '[data-tour="report"]', title: 'Report' }
        });
        await waitFor(() => expect(target.classList.contains('driver-active-element')).toBe(true));

        // Act
        const replacement = document.createElement('button');
        replacement.dataset.tour = 'report';
        replacement.scrollIntoView = vi.fn();
        target.replaceWith(replacement);
        target = replacement;

        // Assert
        await waitFor(() => expect(replacement.classList.contains('driver-active-element')).toBe(true));
        expect(document.querySelectorAll('.product-tour-popover')).toHaveLength(1);
        expect(screen.getByText('Open this report')).toBeTruthy();
    });

    it('releases the page while its target is absent and resumes when the target returns', async () => {
        // Arrange
        const active = productTourCheckpoint.start('app-overview', 'filters', 'user');
        target.dataset.tour = 'event-filters';
        render(ProductTourSpotlight, {
            props: {
                checkpoint: active,
                description: 'Filter reports',
                onDismiss: vi.fn(async () => true),
                target: '[data-tour="event-filters"]',
                title: 'Filters'
            }
        });
        await waitFor(() => expect(target.classList.contains('driver-active-element')).toBe(true));
        await waitFor(() => expect(!!document.querySelector('.driver-overlay')).toBe(true));

        // Act
        target.remove();

        // Assert
        await waitFor(() => {
            expect(!!document.querySelector('.driver-overlay')).toBe(false);
            expect(!!document.querySelector('.product-tour-popover')).toBe(false);
            expect(document.body.classList.contains('driver-active')).toBe(false);
        });
        expect(productTourCheckpoint.current).toBe(active);

        document.body.append(target);

        await waitFor(() => expect(document.querySelectorAll('.product-tour-popover')).toHaveLength(1));
        expect(target.classList.contains('driver-active-element')).toBe(true);
        expect(productTourCheckpoint.current).toBe(active);
    });

    it.each([
        ['filters', 'event-filters'],
        ['saved-views', 'saved-view-trigger']
    ] as const)('preserves a suspended %s step until its target returns', async (checkpointName, targetName) => {
        const active = productTourCheckpoint.start('app-overview', checkpointName, 'user');
        const onDismiss = vi.fn(async () => true);
        target.dataset.tour = targetName;
        render(ProductTourSpotlight, {
            props: { checkpoint: active, description: 'Continue the guide', onDismiss, target: `[data-tour="${targetName}"]`, title: 'Overview' }
        });
        await waitFor(() => expect(target.classList.contains('driver-active-element')).toBe(true));

        // Search suspends the guide, then browser navigation removes the target before Search closes.
        productTourPresentation.suspended = true;
        await waitFor(() => expect(document.querySelector('.product-tour-popover')).toBeNull());
        target.remove();
        productTourPresentation.suspended = false;
        await tick();

        expect(productTourCheckpoint.current).toBe(active);
        expect(document.querySelector('.driver-overlay')).toBeNull();
        expect(onDismiss).not.toHaveBeenCalled();

        document.body.append(target);

        await waitFor(() => expect(target.classList.contains('driver-active-element')).toBe(true));
        expect(document.querySelectorAll('.product-tour-popover')).toHaveLength(1);
        expect(productTourCheckpoint.current).toBe(active);
    });

    it('removes its popover and keyboard listener when unmounted', async () => {
        // Arrange
        const disconnect = vi.fn();
        const onDismiss = vi.fn(async () => true);
        vi.stubGlobal(
            'ResizeObserver',
            class {
                public disconnect = disconnect;
                public observe() {}
            }
        );
        const view = render(ProductTourSpotlight, { props: { checkpoint, description: 'Search', onDismiss, target, title: 'Search' } });
        await screen.findByText('Search', { selector: '.driver-popover-title' });

        // Act
        view.unmount();
        await fireEvent.keyDown(window, { key: 'Escape' });

        // Assert
        expect(onDismiss).not.toHaveBeenCalled();
        expect(document.querySelector('.product-tour-popover')).toBeNull();
    });
});
