import { fireEvent, render, screen, waitFor } from '@testing-library/svelte';
import { describe, expect, it, vi } from 'vitest';

import EventChartSettings from './event-chart-settings.svelte';

vi.mock('$features/organizations/context.svelte', () => ({ organization: { current: 'organization-a' } }));
vi.mock('../api.svelte', () => ({
    getEventMeasurementsQuery: () => ({ data: { measurements: [{ name: 'duration', unit: 'ms' }], truncated: false }, error: null })
}));

describe('event chart settings', () => {
    it('validates a measurement unit and applies a typed chart configuration', async () => {
        const onApply = vi.fn();
        render(EventChartSettings, { chart: null, onApply });
        await fireEvent.click(screen.getByRole('button', { name: 'Configure chart' }));
        await fireEvent.input(screen.getByLabelText('Measurement'), { target: { value: 'duration' } });
        await fireEvent.click(screen.getByRole('button', { name: 'Apply' }));
        await screen.findByText('Select a measurement and its unit together.');
        expect(onApply).not.toHaveBeenCalled();
        await fireEvent.input(screen.getByLabelText('Unit'), { target: { value: 'ms' } });
        await fireEvent.input(screen.getByLabelText('Split by'), { target: { value: 'dimensions.branch' } });
        await fireEvent.click(screen.getByRole('button', { name: 'Apply' }));

        await waitFor(() =>
            expect(onApply).toHaveBeenCalledWith({
                aggregation: 'avg',
                display: 'line',
                group_by: 'dimensions.branch',
                measurement: 'duration',
                mode: 'buckets',
                unit: 'ms'
            })
        );
    });

    it('explicitly restores the default chart', async () => {
        const onApply = vi.fn();
        render(EventChartSettings, { chart: { aggregation: 'avg', display: 'line', measurement: 'duration', mode: 'buckets', unit: 'ms' }, onApply });
        await fireEvent.click(screen.getByRole('button', { name: 'Configure chart' }));
        await fireEvent.click(screen.getByRole('button', { name: 'Default chart' }));

        expect(onApply).toHaveBeenCalledWith(null);
    });
});
