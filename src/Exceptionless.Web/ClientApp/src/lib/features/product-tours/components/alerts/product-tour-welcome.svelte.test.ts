import { fireEvent, render, screen } from '@testing-library/svelte';
import { describe, expect, it, vi } from 'vitest';

import ProductTourWelcome from './product-tour-welcome.svelte';

const recommended = {
    availability: vi.fn(() => ({ available: true })),
    canResume: () => true,
    currentAvailability: { available: true },
    description: 'Learn navigation and search.',
    keywords: ['navigation'],
    name: 'app-overview' as const,
    start: vi.fn(() => ({ checkpointName: 'navigation' as const, route: '/next' })),
    stateKey: 'app_overview' as const,
    title: 'Explore Exceptionless'
};

describe('ProductTourWelcome', () => {
    it('records dismissal from Escape inside the welcome', async () => {
        // Arrange
        const onBrowse = vi.fn();
        const onDismiss = vi.fn();
        const onStart = vi.fn();
        render(ProductTourWelcome, { onBrowse, onDismiss, onStart, open: true, recommended });

        // Act
        await fireEvent.keyDown(screen.getByRole('button', { name: 'Close welcome' }), { key: 'Escape' });
        // Assert
        expect(onBrowse).not.toHaveBeenCalled();
        expect(onDismiss).toHaveBeenCalledOnce();
        expect(onStart).not.toHaveBeenCalled();
    });

    it('provides browse and close choices', async () => {
        // Arrange
        const onBrowse = vi.fn();
        const onDismiss = vi.fn();
        render(ProductTourWelcome, { onBrowse, onDismiss, onStart: vi.fn(), open: true, recommended });

        // Act
        await fireEvent.click(screen.getByRole('button', { name: 'Browse guides' }));
        // Assert
        expect(onBrowse).toHaveBeenCalledOnce();
        // Act
        await fireEvent.click(screen.getByRole('button', { name: 'Close welcome' }));
        // Assert
        expect(onDismiss).toHaveBeenCalledOnce();
    });

    it('offers only the recommended action without a modal or taking focus', async () => {
        // Arrange
        const onStart = vi.fn();
        const focusedElement = document.activeElement;

        // Act
        render(ProductTourWelcome, { onBrowse: vi.fn(), onDismiss: vi.fn(), onStart, open: true, recommended });

        // Assert
        expect(screen.getByRole('region', { name: 'Welcome to Exceptionless' })).toBeTruthy();
        expect(screen.queryByRole('dialog')).toBeNull();
        expect(document.activeElement).toBe(focusedElement);
        expect(screen.getAllByRole('button')).toHaveLength(3);
        expect(screen.getByText(recommended.description)).toBeTruthy();
        // Act
        await fireEvent.click(screen.getByRole('button', { name: recommended.title }));
        // Assert
        expect(onStart).toHaveBeenCalledOnce();
    });

    it('offers setup when that is the recommendation', () => {
        // Arrange: the shared recommendation supplies the guide metadata.

        // Act
        render(ProductTourWelcome, {
            onBrowse: vi.fn(),
            onDismiss: vi.fn(),
            onStart: vi.fn(),
            open: true,
            recommended: { ...recommended, name: 'project-configure', title: 'Configure a project' }
        });

        // Assert
        expect(screen.getByRole('button', { name: 'Continue setup' })).toBeTruthy();
        expect(screen.queryByRole('button', { name: 'Explore Exceptionless' })).toBeNull();
    });

    it('does not dismiss on Escape outside the welcome', async () => {
        // Arrange
        const onDismiss = vi.fn();
        render(ProductTourWelcome, { onBrowse: vi.fn(), onDismiss, onStart: vi.fn(), open: true, recommended });

        // Act
        await fireEvent.keyDown(document.body, { key: 'Escape' });

        // Assert
        expect(onDismiss).not.toHaveBeenCalled();
    });

    it('does not render when closed', () => {
        // Arrange: the shared recommendation supplies the guide metadata.

        // Act
        render(ProductTourWelcome, { onBrowse: vi.fn(), onDismiss: vi.fn(), onStart: vi.fn(), recommended });

        // Assert
        expect(screen.queryByRole('region')).toBeNull();
    });
});
