import { fireEvent, render, screen } from '@testing-library/svelte';
import { describe, expect, it, vi } from 'vitest';

import ProductTourWelcome from './product-tour-welcome.svelte';

const recommended = {
    availability: vi.fn(() => ({ available: true })),
    currentAvailability: { available: true },
    description: 'Learn navigation and search.',
    keywords: ['navigation'],
    name: 'app-overview' as const,
    start: vi.fn(() => ({ checkpointName: 'navigation' as const, route: '/next' })),
    title: 'Explore Exceptionless',
    version: 1
};

describe('ProductTourWelcome', () => {
    it('records dismissal from Escape inside the welcome', async () => {
        const onBrowse = vi.fn();
        const onDismiss = vi.fn();
        const onStart = vi.fn();
        render(ProductTourWelcome, { onBrowse, onDismiss, onStart, open: true, recommended });

        await fireEvent.keyDown(screen.getByRole('button', { name: 'Close welcome' }), { key: 'Escape' });
        expect(onBrowse).not.toHaveBeenCalled();
        expect(onDismiss).toHaveBeenCalledOnce();
        expect(onStart).not.toHaveBeenCalled();
    });

    it('provides browse and close choices', async () => {
        const onBrowse = vi.fn();
        const onDismiss = vi.fn();
        render(ProductTourWelcome, { onBrowse, onDismiss, onStart: vi.fn(), open: true, recommended });

        await fireEvent.click(screen.getByRole('button', { name: 'Browse guides' }));
        expect(onBrowse).toHaveBeenCalledOnce();
        await fireEvent.click(screen.getByRole('button', { name: 'Close welcome' }));
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
        await fireEvent.click(screen.getByRole('button', { name: recommended.title }));
        expect(onStart).toHaveBeenCalledOnce();
    });

    it('offers setup when that is the recommendation', () => {
        render(ProductTourWelcome, {
            onBrowse: vi.fn(),
            onDismiss: vi.fn(),
            onStart: vi.fn(),
            open: true,
            recommended: { ...recommended, name: 'project-configure', title: 'Configure a project' }
        });

        expect(screen.getByRole('button', { name: 'Continue setup' })).toBeTruthy();
        expect(screen.queryByRole('button', { name: 'Explore Exceptionless' })).toBeNull();
    });

    it('does not dismiss unrelated Escape presses or allow actions while saving', async () => {
        const onDismiss = vi.fn();
        render(ProductTourWelcome, { busy: true, onBrowse: vi.fn(), onDismiss, onStart: vi.fn(), open: true, recommended });

        await fireEvent.keyDown(document.body, { key: 'Escape' });
        await fireEvent.keyDown(screen.getByRole('button', { name: 'Close welcome' }), { key: 'Escape' });

        expect(onDismiss).not.toHaveBeenCalled();
        for (const button of screen.getAllByRole('button')) {
            expect(button.hasAttribute('disabled')).toBe(true);
        }
    });

    it('does not render when closed', () => {
        render(ProductTourWelcome, { onBrowse: vi.fn(), onDismiss: vi.fn(), onStart: vi.fn(), recommended });

        expect(screen.queryByRole('region')).toBeNull();
    });
});
