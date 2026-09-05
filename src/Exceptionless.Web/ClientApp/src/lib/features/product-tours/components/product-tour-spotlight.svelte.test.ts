import { appKeyboardShortcuts } from '$features/shared/keyboard-shortcuts';
import { cleanup, fireEvent, render, screen } from '@testing-library/svelte';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';

import type { ProductTourCheckpoint } from '../models';

import ProductTourSpotlight from './product-tour-spotlight.svelte';
vi.mock('../activity', () => ({ submitProductTourActivity: vi.fn() }));

const checkpoint: ProductTourCheckpoint = { checkpointName: 'command-search', source: 'catalog', tourName: 'app-overview', userId: 'user', version: 1 };

describe('ProductTourSpotlight', () => {
    let target: HTMLButtonElement;

    beforeEach(() => {
        vi.stubGlobal(
            'ResizeObserver',
            class {
                disconnect() {}
                observe() {}
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

    it('renders safe text and the shared Kbd component in the driver popover', async () => {
        // Arrange / Act
        render(ProductTourSpotlight, {
            props: {
                checkpoint,
                description: 'Search <not markup>',
                onDismiss: vi.fn(async () => true),
                shortcuts: [{ label: 'Search', shortcut: appKeyboardShortcuts.commandPalette }],
                target,
                title: 'Search'
            }
        });

        // Assert
        expect(await screen.findByText('Search <not markup>')).toBeTruthy();
        const key = screen.getByText('/');
        expect(key.tagName).toBe('KBD');
        expect(key.getAttribute('data-slot')).toBe('kbd');
        expect(screen.queryByRole('button', { name: 'Back' })).toBeNull();
        expect(screen.getByText('Step 2 of 5')).toBeTruthy();
        cleanup();
        expect(document.querySelector('.product-tour-popover')).toBeNull();
    });

    it('omits progress when checkpoints include work outside the guide', async () => {
        // Arrange / Act
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

        // Act / Assert
        await fireEvent.keyUp(window, { key: 'Escape' });
        expect(onDismiss).not.toHaveBeenCalled();
        await fireEvent.keyDown(window, { key: 'Escape' });
        expect(onDismiss).toHaveBeenCalledExactlyOnceWith(checkpoint);
    });
});
