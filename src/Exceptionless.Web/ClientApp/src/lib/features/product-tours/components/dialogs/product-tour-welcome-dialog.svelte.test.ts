import { fireEvent, render, screen } from '@testing-library/svelte';
import { describe, expect, it, vi } from 'vitest';

import ProductTourWelcomeDialog from './product-tour-welcome-dialog.svelte';

const recommended = {
    availability: vi.fn(() => ({ available: true })),
    currentAvailability: { available: true },
    description: 'Learn navigation and search.',
    initialCheckpoint: 'navigation' as const,
    keywords: ['navigation'],
    name: 'ui-overview' as const,
    startingRoute: vi.fn(() => '/next'),
    title: 'Explore Exceptionless',
    version: 1
};

describe('ProductTourWelcomeDialog', () => {
    it('records dismissal from Escape', async () => {
        const onBrowse = vi.fn();
        const onDismiss = vi.fn();
        const onStart = vi.fn();
        render(ProductTourWelcomeDialog, { onBrowse, onDismiss, onStart, open: true, recommended });

        await fireEvent.keyDown(screen.getByRole('dialog'), { key: 'Escape' });
        expect(onBrowse).not.toHaveBeenCalled();
        expect(onDismiss).toHaveBeenCalledOnce();
        expect(onStart).not.toHaveBeenCalled();
    });

    it('provides Browse Guides and Skip choices', async () => {
        const onBrowse = vi.fn();
        const onDismiss = vi.fn();
        render(ProductTourWelcomeDialog, { onBrowse, onDismiss, onStart: vi.fn(), open: true, recommended });

        await fireEvent.click(screen.getByRole('button', { name: 'Browse Guides' }));
        expect(onBrowse).toHaveBeenCalledOnce();
        await fireEvent.click(screen.getByRole('button', { name: 'Skip' }));
        expect(onDismiss).toHaveBeenCalledOnce();
    });
});
