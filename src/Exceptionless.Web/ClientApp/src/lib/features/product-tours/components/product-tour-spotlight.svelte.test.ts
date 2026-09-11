import { cleanup, fireEvent, render, screen, waitFor } from '@testing-library/svelte';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';

import type { ProductTourCheckpoint } from '../models';

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
