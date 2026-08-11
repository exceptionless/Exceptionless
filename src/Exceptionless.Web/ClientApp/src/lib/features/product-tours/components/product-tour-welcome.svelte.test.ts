import { fireEvent, render, screen } from '@testing-library/svelte';
import { describe, expect, it, vi } from 'vitest';

import ProductTourWelcome from './product-tour-welcome.svelte';

const recommended = {
    availability: { available: true },
    description: 'Learn navigation and search.',
    getAvailability: vi.fn(),
    getSteps: vi.fn(),
    id: 'new-ui-overview' as const,
    keywords: ['navigation'],
    title: 'Explore the new UI',
    version: 1
};

describe('ProductTourWelcome', () => {
    it('records only explicit chooser actions', async () => {
        const onBrowse = vi.fn();
        const onSkip = vi.fn();
        const onStart = vi.fn();
        render(ProductTourWelcome, { onBrowse, onSkip, onStart, open: true, recommended });

        await fireEvent.keyDown(screen.getByRole('dialog'), { key: 'Escape' });
        expect(onBrowse).not.toHaveBeenCalled();
        expect(onSkip).not.toHaveBeenCalled();
        expect(onStart).not.toHaveBeenCalled();

        await fireEvent.click(screen.getByRole('button', { name: 'Explore the new UI' }));
        expect(onStart).toHaveBeenCalledOnce();
    });

    it('provides Browse Guides and Skip choices', async () => {
        const onBrowse = vi.fn();
        const onSkip = vi.fn();
        render(ProductTourWelcome, { onBrowse, onSkip, onStart: vi.fn(), open: true, recommended });

        await fireEvent.click(screen.getByRole('button', { name: 'Browse Guides' }));
        expect(onBrowse).toHaveBeenCalledOnce();
        await fireEvent.click(screen.getByRole('button', { name: 'Skip' }));
        expect(onSkip).toHaveBeenCalledOnce();
    });
});
