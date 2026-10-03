import type { Meta, StoryObj } from '@storybook/sveltekit';

import EventMeasurementChart from './event-measurement-chart.svelte';

const meta = {
    args: {
        chart: { aggregation: 'avg', display: 'line', group_by: 'source', measurement: 'duration', mode: 'buckets', unit: 'ms' },
        isLoading: false,
        onSelect: () => {},
        result: {
            interval_milliseconds: 86400000,
            series: [
                {
                    name: 'Account import',
                    points: [
                        { count: 1, date: '2026-10-01T12:00:00Z', value: 120 },
                        { count: 1, date: '2026-10-02T12:00:00Z', value: 100 },
                        { count: 0, date: '2026-10-03T12:00:00Z', value: null },
                        { count: 1, date: '2026-10-04T12:00:00Z', value: 90 },
                        { count: 1, date: '2026-10-05T12:00:00Z', value: 95 }
                    ]
                },
                {
                    name: 'Transaction import',
                    points: [
                        { count: 1, date: '2026-10-01T12:00:00Z', value: 45 },
                        { count: 1, date: '2026-10-02T12:00:00Z', value: 35 },
                        { count: 1, date: '2026-10-03T12:00:00Z', value: 0 },
                        { count: 1, date: '2026-10-04T12:00:00Z', value: 25 },
                        { count: 1, date: '2026-10-05T12:00:00Z', value: 20 }
                    ]
                }
            ],
            total: 8,
            truncated: false
        }
    },
    component: EventMeasurementChart,
    title: 'Components/Events/Measurement chart'
} satisfies Meta<typeof EventMeasurementChart>;

export default meta;
type Story = StoryObj<typeof meta>;
export const Line: Story = {};
export const Bars: Story = { args: { chart: { ...meta.args.chart, display: 'bar' } } };
export const Empty: Story = { args: { result: { series: [], total: 0, truncated: false } } };
export const Limited: Story = { args: { result: { ...meta.args.result, truncated: true } } };
