import type { EventChart } from '$generated/api';

import { describe, expect, it } from 'vitest';

import { chartSeries, chartSignature, eventChartSchema, formatMeasurement, measurementUnit, parseChartParameter } from './event-chart';

const chart: EventChart = { aggregation: 'avg', display: 'line', measurement: 'duration', mode: 'buckets', unit: 'ms' };

describe('event charts', () => {
    it('preserves missing values, real zero values, and coincident observations', () => {
        const series = chartSeries({
            series: [
                {
                    name: 'Import',
                    points: [
                        { count: 1, date: '2026-10-01T12:00:00Z', event_id: 'first', value: 0 },
                        { count: 1, date: '2026-10-01T12:00:00Z', event_id: 'second', value: 12 },
                        { count: 0, date: '2026-10-01T12:05:00Z', value: null }
                    ]
                }
            ],
            total: 3,
            truncated: false
        });
        expect(series[0]!.data.map((point) => point.value)).toEqual([0, 12, null]);
        expect(series[0]!.data.map((point) => point.event_id)).toEqual(['first', 'second', undefined]);
        expect(formatMeasurement(0, 'ms')).toBe('0 ms');
        expect(formatMeasurement(null, 'ms')).toBe('—');
    });

    it('distinguishes an explicit default chart from an absent or invalid URL override', () => {
        expect(parseChartParameter(JSON.stringify(chart))).toEqual(chart);
        expect(parseChartParameter('null')).toBeNull();
        expect(parseChartParameter(null)).toBeUndefined();
        expect(parseChartParameter('{invalid')).toBeUndefined();
        expect(parseChartParameter(JSON.stringify({ ...chart, unit: null }))).toBeUndefined();
    });

    it('requires a unit for measurements and allows generic event counts without a measurement', () => {
        expect(eventChartSchema.safeParse({ ...chart, unit: null }).success).toBe(false);
        expect(eventChartSchema.safeParse({ ...chart, aggregation: 'count', measurement: null, unit: null }).success).toBe(true);
        expect(eventChartSchema.safeParse({ ...chart, aggregation: 'count', measurement: null, mode: 'events', unit: null }).success).toBe(false);
        expect(eventChartSchema.safeParse({ ...chart, group_by: 'labels.branch', unit: '{call}' }).success).toBe(true);
        expect(eventChartSchema.safeParse({ ...chart, group_by: 'result' }).success).toBe(true);
        expect(eventChartSchema.safeParse({ ...chart, group_by: 'dimensions.branch' }).success).toBe(false);
    });

    it('formats counts independently of the measured unit', () => {
        expect(measurementUnit({ ...chart, aggregation: 'count' })).toBe('1');
        expect(measurementUnit({ ...chart, aggregation: 'count', mode: 'events' })).toBe('ms');
    });

    it('compares chart configurations without depending on property order or absent optional fields', () => {
        expect(chartSignature(chart)).toBe(
            chartSignature({ aggregation: 'avg', display: 'line', group_by: null, measurement: 'duration', mode: 'buckets', unit: 'ms' })
        );
        expect(chartSignature(chart)).not.toBe(chartSignature({ ...chart, unit: 's' }));
        expect(chartSignature(null)).toBe(chartSignature(undefined));
    });
});
