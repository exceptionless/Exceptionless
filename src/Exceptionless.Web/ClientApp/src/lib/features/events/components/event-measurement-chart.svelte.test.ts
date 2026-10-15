import type { EventChart, EventChartResult } from '$generated/api';

import { fireEvent, render, screen } from '@testing-library/svelte';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';

import EventMeasurementChart from './event-measurement-chart.svelte';

const chart: EventChart = { aggregation: 'avg', display: 'line', measurement: 'duration', mode: 'events', unit: 'ms' };

describe('EventMeasurementChart', () => {
    beforeEach(() => {
        vi.stubGlobal(
            'ResizeObserver',
            class {
                public disconnect() {}
                public observe() {}
                public unobserve() {}
            }
        );
    });

    afterEach(() => vi.unstubAllGlobals());

    it('keeps coincident events separately accessible and preserves zeros and missing values', async () => {
        const onSelect = vi.fn();
        const result: EventChartResult = {
            series: [
                {
                    name: 'Import',
                    points: [
                        { count: 1, date: '2026-10-01T12:00:00Z', event_id: 'zero', value: 0 },
                        { count: 1, date: '2026-10-01T12:00:00Z', event_id: 'ten', value: 10 },
                        { count: 0, date: '2026-10-01T12:01:00Z', value: null }
                    ]
                }
            ],
            total: 2,
            truncated: true
        };

        render(EventMeasurementChart, { chart, isLoading: false, onSelect, result });
        await fireEvent.click(screen.getByText('View observations'));
        await fireEvent.click(screen.getByRole('button', { name: /10 ms/ }));

        expect(onSelect).toHaveBeenCalledWith(expect.objectContaining({ event_id: 'ten', value: 10 }), 'Import');
        expect(screen.getByText('0 ms').closest('button')).toBeTruthy();
        expect(screen.getByRole('button', { name: /—/ }).hasAttribute('disabled')).toBe(true);
        expect(screen.getByRole('status').textContent).toContain('limited set');
    });

    it('explains an empty measurement selection without rendering a false zero', () => {
        render(EventMeasurementChart, { chart, isLoading: false, onSelect: vi.fn(), result: { series: [], total: 0, truncated: false } });
        expect(screen.getByText('No matching observations in this time range.')).toBeTruthy();
        expect(screen.queryByText('0 ms')).toBeNull();
    });
});
