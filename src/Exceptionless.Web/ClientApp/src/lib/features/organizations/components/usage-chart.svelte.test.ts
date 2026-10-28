import { render, waitFor } from '@testing-library/svelte';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';

import { createUsageChartData } from './usage-chart.fixtures.ts';
import UsageChart from './usage-chart.svelte';

describe('Usage chart', () => {
    beforeEach(() => {
        // Supply layout measurements missing from jsdom; render the real chart and scales.
        vi.spyOn(HTMLElement.prototype, 'clientWidth', 'get').mockReturnValue(1000);
        vi.spyOn(HTMLElement.prototype, 'clientHeight', 'get').mockReturnValue(250);
        vi.stubGlobal(
            'ResizeObserver',
            class {
                public disconnect() {}
                public observe() {}
                public unobserve() {}
            }
        );
    });

    afterEach(() => {
        vi.restoreAllMocks();
        vi.unstubAllGlobals();
    });

    it.each([false, true])('keeps the limit independent of randomized usage (project=%s)', async (showOrganizationTotal) => {
        // Arrange
        const data = createUsageChartData(showOrganizationTotal ? 912 : 731);

        // Act
        const { container } = render(UsageChart, { data, showOrganizationTotal });

        // Assert
        await waitFor(() => {
            const path = container.querySelector('path.usage-limit-line:not(.lc-area-path)')?.getAttribute('d');
            expect(path).toBeTruthy();
            // All control points of a horizontal line must have the same y coordinate.
            const coordinates = path!.match(/-?\d+(?:\.\d+)?/g)!.map(Number);
            const y = coordinates.filter((_, index) => index % 2 === 1);
            expect(y.length).toBeGreaterThan(2);
            expect(Math.max(...y) - Math.min(...y)).toBeLessThan(0.01);
            expect(Math.min(...y)).toBeGreaterThanOrEqual(0);

            // Project and organization totals are comparisons, not additive layers.
            const baseline = Number(container.querySelector('.lc-rule-y-line')!.getAttribute('y1'));
            const lines = container.querySelectorAll('path.lc-area-line');
            const firstY = Number(lines[0]!.getAttribute('d')!.match(/^M[^,]+,(-?[\d.]+)/)![1]);
            const firstPoint = data[0]!;
            const firstValue = showOrganizationTotal ? firstPoint.organization_total : firstPoint.total;
            expect((baseline - firstY) / (baseline - y[0]!)).toBeCloseTo(firstValue / firstPoint.limit, 3);
            if (showOrganizationTotal) {
                const totalY = Number(lines[1]!.getAttribute('d')!.match(/^M[^,]+,(-?[\d.]+)/)![1]);
                expect((baseline - totalY) / (baseline - y[0]!)).toBeCloseTo(firstPoint.total / firstPoint.limit, 3);
            }
        });
    });

    it.each([false, true])('plots changing limits and usage above the allowance on the same scale (project=%s)', async (showOrganizationTotal) => {
        // Arrange
        const data = createUsageChartData(481).map((point, index) => ({ ...point, limit: index < 6 ? 500_000 : 1_000_000 }));

        // Act
        const { container } = render(UsageChart, { data, showOrganizationTotal });

        // Assert
        await waitFor(() => {
            const path = container.querySelector('path.usage-limit-line:not(.lc-area-path)')?.getAttribute('d');
            expect(path).toBeTruthy();
            const coordinates = path!.match(/-?\d+(?:\.\d+)?/g)!.map(Number);
            const y = coordinates.filter((_, index) => index % 2 === 1);
            const baseline = Number(container.querySelector('.lc-rule-y-line')!.getAttribute('y1'));
            expect((baseline - y.at(-1)!) / (baseline - y[0]!)).toBeCloseTo(2, 3);
            expect(Math.min(...y)).toBeGreaterThanOrEqual(0);
            expect(Math.max(...y)).toBeLessThanOrEqual(baseline);
        });
    });
});
