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
    steps: [],
    version: 1
};

beforeEach(() => {
    Object.defineProperty(Element.prototype, 'animate', { configurable: true, value: vi.fn(() => ({ cancel() {}, finished: Promise.resolve() })) });
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
    Reflect.deleteProperty(Element.prototype, 'animate');
});

describe('ProductTourActivity', () => {
    it('makes exact daily counts keyboard accessible without relying on color', async () => {
        // Arrange
        render(ProductTourActivity, {
            end: '2026-01-03T00:00:00Z',
            interval: ProductTourUsageInterval.Day,
            start: '2026-01-01T00:00:00Z',
            tour: { ...tour, activity: [...tour.activity, { completed: 0, date_utc: '2026-01-02T00:00:00Z', dismissed: 0, shown: 0, started: 0 }] }
        });
        const chart = screen.getByRole('slider');

        // Act
        await fireEvent.keyDown(chart, { key: 'Home' });

        // Assert
        expect(chart.getAttribute('aria-valuenow')).toBe('0');
        expect(chart.getAttribute('aria-valuetext')).toContain('Completed: 1');
        await fireEvent.keyDown(chart, { key: 'ArrowRight' });
        expect(chart.getAttribute('aria-valuenow')).toBe('1');
        expect(chart.getAttribute('aria-valuetext')).toContain('Completed: 0');
    });

    it('labels invitation acceptance without a redundant started series', () => {
        // Act
        render(ProductTourActivity, { end: '2026-02-01T00:00:00Z', interval: ProductTourUsageInterval.Month, tour: { ...tour, kind: ProductTourKind.Prompt } });

        // Assert
        expect(screen.getByLabelText('Period totals').textContent).toContain('Accepted');
        expect(screen.getByLabelText('Period totals').textContent).not.toContain('Started');
    });
    it('shows the chart without disclosures while preserving screen-reader access to values', () => {
        // Act
        render(ProductTourActivity, { end: '2026-02-01T00:00:00Z', interval: ProductTourUsageInterval.Month, start: '2026-01-01T00:00:00Z', tour });

        // Assert
        expect(screen.getByLabelText(/Monthly guide activity/)).toBeTruthy();
        expect(screen.queryByText('View details')).toBeNull();
        const table = screen.getByRole('table', { name: 'Guide activity by date' });
        expect(table.closest('.sr-only')).not.toBeNull();
        expect(table.textContent).toContain('Jan 2026');
        expect(table.closest('details')).toBeNull();
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
