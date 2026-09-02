import type { ProductTourSummary } from '$generated/api';

import { ProductTourKind, ProductTourLaunchSource, ProductTourUsageInterval } from '$generated/api';
import { cleanup, fireEvent, render, screen } from '@testing-library/svelte';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';

import ProductTourActivity from './product-tour-activity.svelte';

const tour: ProductTourSummary = {
    activity: [{ completed: 1, date_utc: '2026-01-01T00:00:00Z', dismissed: 0, shown: 0, started: 1 }],
    completed: 1,
    dismissed: 0,
    kind: ProductTourKind.Guide,
    name: 'app-overview',
    shown: 0,
    start_sources: [{ count: 1, source: ProductTourLaunchSource.CommandPalette }],
    started: 1,
    version: 1
};

beforeEach(() => {
    vi.stubGlobal(
        'ResizeObserver',
        class {
            disconnect() {}
            observe() {}
            unobserve() {}
        }
    );
});

afterEach(() => {
    cleanup();
    vi.unstubAllGlobals();
});

describe('ProductTourActivity', () => {
    it('leads with the chart and keeps exact totals and rows behind details', async () => {
        // Act
        render(ProductTourActivity, { end: '2026-02-01T00:00:00Z', interval: ProductTourUsageInterval.Month, start: '2026-01-01T00:00:00Z', tour });

        // Assert
        expect(screen.getByLabelText(/Monthly guide activity/)).toBeTruthy();
        const details = screen.getByText('View details').closest('details');
        expect(details?.open).toBe(false);
        expect(details?.contains(screen.getByRole('table', { hidden: true }))).toBe(true);

        // Act
        await fireEvent.click(screen.getByText('View details'));

        // Assert
        expect(details?.open).toBe(true);
        expect(screen.getByText('100.0% of starts')).toBeTruthy();
        expect(screen.getByText(/Command palette:/)).toBeTruthy();
    });

    it('shows a collection empty state instead of zero-valued completion metrics', () => {
        // Arrange
        const empty = { ...tour, activity: [], completed: 0, start_sources: [], started: 0 };

        // Act
        render(ProductTourActivity, { end: '2026-02-01T00:00:00Z', interval: ProductTourUsageInterval.Month, tour: empty });

        // Assert
        expect(screen.getByText('No recorded activity in this period.')).toBeTruthy();
        expect(screen.queryByLabelText('Selected guide totals')).toBeNull();
        expect(screen.queryByText('View details')).toBeNull();
    });
});
