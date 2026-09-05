import type { ProductTourSummary } from '$generated/api';

import { ProductTourKind, ProductTourLaunchSource } from '$generated/api';
import { cleanup, fireEvent, render, screen } from '@testing-library/svelte';
import { afterEach, describe, expect, it } from 'vitest';

import ProductTourActivityPopover from './product-tour-activity-popover.svelte';

const tour: ProductTourSummary = {
    activity: [],
    completed: 2,
    dismissed: 1,
    kind: ProductTourKind.Guide,
    name: 'app-overview',
    shown: 0,
    start_sources: [{ count: 4, source: ProductTourLaunchSource.CommandPalette }],
    started: 4,
    version: 1
};

afterEach(cleanup);

describe('ProductTourActivityPopover', () => {
    it('keeps diagnostic counts behind a named info button', async () => {
        // Arrange
        render(ProductTourActivityPopover, { title: 'Explore Exceptionless', tour });
        expect(screen.queryByText(/Most common exit/)).toBeNull();

        // Act
        await fireEvent.click(screen.getByRole('button', { name: 'Explore Exceptionless activity details' }));

        // Assert
        expect(screen.getByRole('dialog', { name: 'Explore Exceptionless' })).toBeTruthy();
        expect(screen.getByRole('list', { name: 'Guide entry points' }).textContent).toContain('100.0%');
    });

    it('explains missing diagnostics without inventing step counts', async () => {
        // Arrange
        render(ProductTourActivityPopover, { title: 'Explore Exceptionless', tour: { ...tour, start_sources: [] } });

        // Act
        await fireEvent.click(screen.getByRole('button'));

        // Assert
        expect(screen.getByText('No entry-point activity recorded in this period.')).toBeTruthy();
        expect(screen.queryByText(/Most common exit/)).toBeNull();
    });

    it('explains invitation acceptance without presenting guide steps', async () => {
        // Arrange
        render(ProductTourActivityPopover, { title: 'Welcome invitation', tour: { ...tour, kind: ProductTourKind.Prompt } });

        // Act
        await fireEvent.click(screen.getByRole('button'));

        // Assert
        expect(screen.getByText(/Accepted counts invitations used to open a guide/)).toBeTruthy();
        expect(screen.queryByRole('list', { name: 'Guide entry points' })).toBeNull();
    });
});
